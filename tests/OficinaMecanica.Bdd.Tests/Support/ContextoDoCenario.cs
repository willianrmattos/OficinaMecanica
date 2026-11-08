using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Tests.Common.Builders;

namespace OficinaMecanica.Bdd.Tests.Support;

// Tudo o que um cenario precisa: a API no ar, o cliente HTTP autenticado, o harness do bus e os dados da oficina. Cada
// cenario cria o proprio contexto (uma fabrica nova, um banco novo, um relogio novo), entao nenhum depende da ordem nem do estado de outro.
public sealed class ContextoDoCenario : IAsyncDisposable
{
    // Token ja cancelado: faz Select() devolver so o que ja foi registrado, sem esperar mensagens novas. As verificacoes de
    // "nao enviou" so rodam depois de a saga chegar ao estado final, quando tudo o que ela enviou ja esta registrado.
    private static readonly CancellationToken SemEspera = new(canceled: true);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private FabricaDaApi? _fabrica;
    private HttpClient? _http;
    private ITestHarness? _harness;
    private CapturadorDeMetricas? _metricas;

    public Dictionary<string, Servico> Servicos { get; } = new();
    public Dictionary<string, Peca> Pecas { get; } = new();
    public Cliente? Cliente { get; private set; }
    public Veiculo? Veiculo { get; private set; }

    public Guid OrdemId { get; private set; }
    public string NumeroDaOrdem { get; private set; } = string.Empty;
    public Guid OrcamentoId { get; } = Guid.NewGuid();
    public Guid PagamentoId { get; } = Guid.NewGuid();
    public decimal ValorDoOrcamento { get; private set; }
    public string LinkDePagamento => $"https://pagamento.exemplo.com/checkout/{PagamentoId:N}";
    public HttpStatusCode? UltimoStatusHttp { get; set; }
    public int PrazosPublicadosPeloSweeper { get; private set; }

    public FabricaDaApi Fabrica => _fabrica ?? throw new InvalidOperationException("A API do cenário ainda não foi iniciada.");
    public RoteiroDosServicos Roteiro => Fabrica.Roteiro;
    public RegistroDeEmails Emails => Fabrica.Emails;
    public ITestHarness Harness => _harness ?? throw new InvalidOperationException("O harness do cenário ainda não foi iniciado.");

    public CapturadorDeMetricas Metricas => _metricas ?? throw new InvalidOperationException("O capturador de métricas do cenário ainda não foi iniciado.");

    public async Task IniciarAsync()
    {
        _metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        _fabrica = new FabricaDaApi();
        _http = _fabrica.CreateClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GerarToken());

        _harness = _fabrica.Services.GetRequiredService<ITestHarness>();
        _harness.TestTimeout = Esperar.Teto;
        await _harness.Start();
    }

    private string GerarToken()
    {
        var credenciais = new SigningCredentials(
            new RsaSecurityKey(Fabrica.ChaveDeAssinatura) { KeyId = FabricaDaApi.KidDeTeste },
            SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "OficinaMecanica.Seguranca",
            audience: "OficinaMecanica.Client",
            claims: new[] { new Claim(ClaimTypes.Name, "atendente"), new Claim(ClaimTypes.Role, "Admin") },
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ---- Dados da oficina (semeados direto no banco: cadastro nao e o assunto da saga) ----

    public async Task CadastrarServicoAsync(string nome, decimal preco)
    {
        var servico = new ServicoBuilder().ComNome(nome).ComPreco(preco).Build();
        Servicos[nome] = servico;
        await SemearAsync(servico);
    }

    public async Task CadastrarPecaAsync(string nome, decimal preco, int estoque)
    {
        var peca = new PecaBuilder().ComNome(nome).ComPreco(preco).ComEstoque(estoque).Build();
        Pecas[nome] = peca;
        await SemearAsync(peca);
    }

    public async Task CadastrarClienteComVeiculoAsync(string nome, string placa)
    {
        Cliente = new ClienteBuilder().ComNome(nome).Build();
        Veiculo = new VeiculoBuilder().DoCliente(Cliente.Id).ComPlaca(placa).Build();
        await SemearAsync(Cliente, Veiculo);
    }

    private async Task SemearAsync(params object[] entidades)
    {
        await using var escopo = Fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(entidades);
        await db.SaveChangesAsync();
    }

    // Tira do estoque o que sobrou da peca depois de a OS ser aberta (a abertura so valida o estoque), para o cliente aprovar
    // quando ja nao ha peca suficiente.
    public async Task ReduzirEstoqueDaPecaParaAsync(string peca, int quantidade)
    {
        var id = Pecas[peca].Id;
        await using var escopo = Fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        var entidade = await db.Pecas.SingleAsync(p => p.Id == id);
        entidade.RemoverEstoque(entidade.QuantidadeEstoque - quantidade);
        await db.SaveChangesAsync();
    }

    // ---- Caminho HTTP (o que o atendente e o cliente fazem de verdade) ----

    public async Task AbrirOrdemAsync(string servico, int quantidadeDoServico, string peca, int quantidadeDaPeca)
    {
        var resposta = await _http!.PostAsJsonAsync("/api/ordens-de-servico", new
        {
            ClienteId = Cliente!.Id,
            VeiculoId = Veiculo!.Id,
            Observacoes = "Barulho ao frear.",
            Servicos = new[] { new { ServicoId = Servicos[servico].Id, Quantidade = quantidadeDoServico } },
            Pecas = new[] { new { PecaId = Pecas[peca].Id, Quantidade = quantidadeDaPeca } }
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, "a ordem de serviço deveria ser aberta");
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        OrdemId = corpo.GetProperty("id").GetGuid();
        NumeroDaOrdem = corpo.GetProperty("numero").GetString()!;
    }

    public async Task<HttpStatusCode> AprovarOrcamentoAsync() =>
        Registrar(await _http!.PostAsync($"/api/ordens-de-servico/{OrdemId}/aprovar", content: null));

    public async Task<HttpStatusCode> RecusarOrcamentoAsync(string motivo) =>
        Registrar(await _http!.PostAsJsonAsync($"/api/ordens-de-servico/{OrdemId}/recusar", new { Motivo = motivo }));

    public async Task<HttpStatusCode> CancelarOrdemAsync(string motivo) =>
        Registrar(await _http!.PostAsJsonAsync($"/api/ordens-de-servico/{OrdemId}/cancelar", new { Motivo = motivo }));

    private HttpStatusCode Registrar(HttpResponseMessage resposta)
    {
        UltimoStatusHttp = resposta.StatusCode;
        return resposta.StatusCode;
    }

    public async Task<JsonElement> ConsultarOrdemAsync()
    {
        var resposta = await _http!.GetAsync($"/api/ordens-de-servico/{OrdemId}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    public async Task<int> EstoqueDaPecaAsync(string peca)
    {
        var resposta = await _http!.GetAsync($"/api/pecas/{Pecas[peca].Id}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("quantidadeEstoque").GetInt32();
    }

    // ---- Billing e Execucao (eventos que os servicos publicam) ----

    public async Task PublicarComoServicoAsync<T>(T evento) where T : class => await Harness.Bus.Publish(evento);

    public Task ExecucaoIniciaODiagnosticoAsync() =>
        PublicarComoServicoAsync(new DiagnosticoIniciadoBuilder().DaOrdem(OrdemId).Build());

    public Task ExecucaoConcluiODiagnosticoAsync(string laudo) =>
        PublicarComoServicoAsync(new DiagnosticoConcluidoBuilder().DaOrdem(OrdemId).ComLaudo(laudo).Build());

    public Task BillingAprovaOPagamentoAsync() =>
        PublicarComoServicoAsync(new PagamentoAprovadoBuilder().DaOrdem(OrdemId).ComPagamento(PagamentoId).Build());

    public Task BillingCancelaOPagamentoAsync() =>
        PublicarComoServicoAsync(new PagamentoCanceladoBuilder().DaOrdem(OrdemId).ComPagamento(PagamentoId).Build());

    public Task BillingEstornaOPagamentoAsync() =>
        PublicarComoServicoAsync(new PagamentoEstornadoBuilder().DaOrdem(OrdemId).ComPagamento(PagamentoId).Build());

    public Task ExecucaoFinalizaOReparoAsync() =>
        PublicarComoServicoAsync(new ExecucaoFinalizadaBuilder().DaOrdem(OrdemId).Build());

    public Task ExecucaoInviabilizaOReparoAsync(string motivo) =>
        PublicarComoServicoAsync(new ExecucaoInviavelBuilder().DaOrdem(OrdemId).ComMotivo(motivo).Build());

    // O Billing que gera o orcamento e o link de pagamento assim que recebe o pedido da saga.
    public void ProgramarBillingCooperativo()
    {
        Roteiro.AoReceber<GerarOrcamento>(pedido =>
        {
            ValorDoOrcamento = pedido.ValorTotal;
            return new OrcamentoGeradoBuilder()
                .DaOrdem(pedido.OrdemDeServicoId)
                .ComOrcamento(OrcamentoId)
                .ComValorTotal(pedido.ValorTotal)
                .ValidoAte(pedido.ValidoAte)
                .ComOcorridoEm(Fabrica.Relogio.GetUtcNow())
                .Build();
        });

        Roteiro.AoReceber<SolicitarPagamento>(pedido => new PagamentoSolicitadoBuilder()
            .DaOrdem(pedido.OrdemDeServicoId)
            .ComPagamento(PagamentoId)
            .ComLink(LinkDePagamento)
            .ExpiraEm(pedido.ExpiraEm)
            .ComOcorridoEm(Fabrica.Relogio.GetUtcNow())
            .Build());
    }

    // ---- Caminhos compostos (cada passo e um evento real, nao um atalho no banco) ----

    public async Task ConduzirAteAguardandoAprovacaoAsync(string servico, string peca, int quantidadeDaPeca)
    {
        await AbrirOrdemAsync(servico, 1, peca, quantidadeDaPeca);
        await AguardarSagaEmAsync("AguardandoDiagnostico");
        await ExecucaoIniciaODiagnosticoAsync();
        await AguardarSagaEmAsync("EmDiagnostico");
        await ExecucaoConcluiODiagnosticoAsync("Pastilhas de freio gastas.");
        await AguardarSagaEmAsync("AguardandoAprovacao");
    }

    public async Task ConduzirAteAguardandoPagamentoAsync(string servico, string peca, int quantidadeDaPeca)
    {
        await ConduzirAteAguardandoAprovacaoAsync(servico, peca, quantidadeDaPeca);
        (await AprovarOrcamentoAsync()).Should().Be(HttpStatusCode.Accepted);
        await AguardarSagaEmAsync("AguardandoPagamento");
        await Esperar.AteQue(() => Roteiro.RecebidosDoTipo<SolicitarPagamento>().Count, n => n == 1, "o Billing receber SolicitarPagamento");
        await AguardarLinkDePagamentoAsync();
    }

    public async Task ConduzirAteEmExecucaoAsync(string servico, string peca, int quantidadeDaPeca)
    {
        await ConduzirAteAguardandoPagamentoAsync(servico, peca, quantidadeDaPeca);
        await BillingAprovaOPagamentoAsync();
        await AguardarSagaEmAsync("EmExecucao");
    }

    // O link chega a OS quando a saga processa o PagamentoSolicitado do Billing, depois de a saga entrar em AguardandoPagamento:
    // quem le o link logo apos a transicao precisa esperar, nao ler uma vez.
    public async Task<string?> AguardarLinkDePagamentoAsync()
    {
        var esperado = PagamentoId.ToString("N");
        return await Esperar.AteAsync(
            async () => (await ConsultarOrdemAsync()).GetProperty("linkPagamento").GetString(),
            link => link is not null && link.Contains(esperado),
            "o link de pagamento do Billing chegar à ordem de serviço");
    }

    // ---- Tempo e sweeper ----

    public async Task AvancarORelogioERodarOSweeperAsync(int horas)
    {
        Fabrica.Relogio.Advance(TimeSpan.FromHours(horas));

        await using var escopo = Fabrica.Services.CreateAsyncScope();
        PrazosPublicadosPeloSweeper = await escopo.ServiceProvider.GetRequiredService<SweeperDePrazos>().ProcessarLoteAsync();
    }

    // ---- Observacao ----

    public OrdemDeServicoSagaState? SagaDaOrdem() =>
        Harness.GetSagaStateMachineHarness<OrdemDeServicoStateMachine, OrdemDeServicoSagaState>().Sagas.Contains(OrdemId);

    public Task<string> AguardarSagaEmAsync(string estado) =>
        Esperar.AteQue(() => SagaDaOrdem()?.CurrentState ?? "(a saga ainda não existe)", atual => atual == estado, $"a saga da ordem em '{estado}'");

    public bool SagaConsumiuFalhaDe<TComando>() where TComando : class =>
        Harness.GetSagaStateMachineHarness<OrdemDeServicoStateMachine, OrdemDeServicoSagaState>()
            .Consumed.Select<Fault<TComando>>(SemEspera).Any();

    // O que a saga enviou (registrado no instante do envio, antes de o servico de destino consumir): serve as verificacoes
    // de "nao enviou", que nao podem passar so porque o consumer ainda nao recebeu a mensagem.
    public IReadOnlyList<string> ComandosEnviadosPelaSaga() =>
        Harness.Sent.Select(_ => true, SemEspera).Select(x => x.MessageType.Name).ToList();

    public async Task<OrdemDeServico> OrdemNoBancoAsync()
    {
        await using var escopo = Fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OrdensDeServico.AsNoTracking().SingleAsync(o => o.Id == OrdemId);
    }

    public async Task<List<SagaPrazo>> PrazosDaOrdemAsync()
    {
        await using var escopo = Fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SagaPrazos.AsNoTracking().Where(p => p.CorrelationId == OrdemId).ToListAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _http?.Dispose();
        _metricas?.Dispose();

        if (_fabrica is not null)
            await _fabrica.DisposeAsync();
    }
}
