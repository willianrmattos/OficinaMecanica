using FluentAssertions;
using MassTransit;
using MassTransit.Events;
using MassTransit.Metadata;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.Commands.CriarOrdemDeServico;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// Uma OS real (criada pelo command da Application, que publica OrdemDeServicoAberta) com uma peca em estoque, e os passos do
// fluxo como metodos: cada um publica o evento que o Billing ou a Execucao publicaria e espera a saga processa-lo. Assim o
// teste de cada transicao escreve so o que e dela e parte de um estado alcancado pelo caminho de verdade.
public sealed class CenarioDaSaga : IAsyncDisposable
{
    // Token ja cancelado: faz Select() devolver so o que ja foi gravado, sem esperar mensagens novas (as leituras do harness
    // so sao feitas depois de a saga consumir o evento, quando tudo o que ela enviou ja esta registrado).
    private static readonly CancellationToken SemEspera = new(canceled: true);

    public const int EstoqueInicial = 10;
    public const int QuantidadeDaPeca = 3;
    public const decimal PrecoDoServico = 150m;
    public const decimal PrecoDaPeca = 40m;
    public const decimal ValorTotalDaOs = PrecoDoServico + QuantidadeDaPeca * PrecoDaPeca;
    public const string NomeDoServico = "Troca de óleo";
    public const string NomeDaPeca = "Filtro de óleo";

    private CenarioDaSaga(AmbienteDaSaga ambiente)
    {
        Ambiente = ambiente;
    }

    public AmbienteDaSaga Ambiente { get; }
    public Guid OrdemId { get; private set; }
    public Guid ClienteId { get; private set; }
    public Guid PecaId { get; private set; }
    public Guid ServicoId => _servicoId;
    public Guid OrcamentoId { get; } = Guid.NewGuid();
    public Guid PagamentoId { get; } = Guid.NewGuid();
    public OrdemDeServicoStateMachine Maquina => Ambiente.Saga.StateMachine;

    public static async Task<CenarioDaSaga> CriarAsync(
        int estoque = EstoqueInicial,
        Action<IServiceCollection>? ajustarServicos = null,
        params (string Chave, string? Valor)[] configuracao)
    {
        var ambiente = await AmbienteDaSaga.CriarAsync(ajustarServicos, configuracao);
        var cenario = new CenarioDaSaga(ambiente);

        var cliente = new ClienteBuilder().Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();
        var servico = new ServicoBuilder().ComNome(NomeDoServico).ComPreco(PrecoDoServico).Build();
        var peca = new PecaBuilder().ComNome(NomeDaPeca).ComPreco(PrecoDaPeca).ComEstoque(estoque).Build();

        await ambiente.ConsultarAsync(async db =>
        {
            db.AddRange(cliente, veiculo, servico, peca);
            return await db.SaveChangesAsync();
        });

        cenario.ClienteId = cliente.Id;
        cenario.PecaId = peca.Id;
        cenario._veiculoId = veiculo.Id;
        cenario._servicoId = servico.Id;
        return cenario;
    }

    private Guid _veiculoId;
    private Guid _servicoId;

    // Abre a OS pelo caminho real (command + publicacao de OrdemDeServicoAberta) e espera a saga nascer.
    public async Task AbrirAsync()
    {
        var ordem = await Ambiente.EnviarAsync(new CriarOrdemDeServicoCommand(
            ClienteId,
            _veiculoId,
            "Barulho ao frear.",
            new List<ItemServicoInput> { new(_servicoId, 1) },
            new List<ItemPecaInput> { new(PecaId, QuantidadeDaPeca) }));

        OrdemId = ordem.Id;
        await AguardarEstadoAsync(m => m.AguardandoDiagnostico);
    }

    public async Task ChegarEmAguardandoPagamentoAsync()
    {
        await AbrirAsync();
        await IniciarDiagnosticoAsync();
        await ConcluirDiagnosticoAsync();
        await GerarOrcamentoAsync();
        await AprovarAsync();
    }

    public async Task ChegarEmExecucaoAsync()
    {
        await ChegarEmAguardandoPagamentoAsync();
        await SolicitarPagamentoAsync();
        await PagarAsync();
    }

    public async Task ChegarEmCancelandoPagamentoAsync(bool comPagamentoSolicitado = true)
    {
        await ChegarEmAguardandoPagamentoAsync();
        if (comPagamentoSolicitado)
            await SolicitarPagamentoAsync();
        await ExpirarPagamentoAsync();
    }

    public async Task ChegarEmEstornandoAsync()
    {
        await ChegarEmExecucaoAsync();
        await InviabilizarExecucaoAsync();
    }

    public async Task ChegarEmEstornoPendenteManualAsync()
    {
        await ChegarEmEstornandoAsync();
        await FalharEstornoAsync();
    }

    // Os tres finais, alcancados pelo caminho de cada um.
    public async Task ChegarEmConcluidaAsync()
    {
        await ChegarEmExecucaoAsync();
        await PublicarAsync(new ExecucaoFinalizadaBuilder().DaOrdem(OrdemId).Build());
        await AguardarEstadoAsync(m => m.Concluida);
    }

    public async Task ChegarEmCanceladaAsync()
    {
        await ChegarEmAguardandoPagamentoAsync();
        await SolicitarPagamentoAsync();
        await PublicarAsync(new PagamentoNaoSolicitadoBuilder().DaOrdem(OrdemId).Build());
        await AguardarEstadoAsync(m => m.Cancelada);
    }

    public async Task ChegarEmOrcamentoRecusadoAsync()
    {
        await AbrirAsync();
        await IniciarDiagnosticoAsync();
        await ConcluirDiagnosticoAsync();
        await GerarOrcamentoAsync();
        await PublicarAsync(new ClienteRecusouOrcamentoBuilder().DaOrdem(OrdemId).Build());
        await AguardarEstadoAsync(m => m.OrcamentoRecusado);
    }

    // Tira o estoque que sobrou da peca depois de a OS ser criada (a criacao valida o estoque), para o cliente aprovar quando
    // ja nao ha peca suficiente.
    public Task ReduzirEstoqueParaAsync(int quantidade) =>
        Ambiente.ConsultarAsync(async db =>
        {
            var peca = await db.Pecas.SingleAsync(p => p.Id == PecaId);
            peca.RemoverEstoque(peca.QuantidadeEstoque - quantidade);
            return await db.SaveChangesAsync();
        });

    // OS gravada direto no banco, sem publicar OrdemDeServicoAberta: serve a quem precisa de um evento chegando ANTES de a saga existir.
    public async Task<Guid> CriarOrdemSemAbrirASagaAsync()
    {
        var ordem = new OrdemDeServicoBuilder().DoCliente(ClienteId).DoVeiculo(_veiculoId).Recebida();
        await Ambiente.ConsultarAsync(async db =>
        {
            db.Add(ordem);
            return await db.SaveChangesAsync();
        });

        OrdemId = ordem.Id;
        return ordem.Id;
    }

    public async Task IniciarDiagnosticoAsync()
    {
        await PublicarAsync(new DiagnosticoIniciadoBuilder().DaOrdem(OrdemId).Build());
        await AguardarEstadoAsync(m => m.EmDiagnostico);
    }

    public async Task ConcluirDiagnosticoAsync()
    {
        await PublicarAsync(new DiagnosticoConcluidoBuilder().DaOrdem(OrdemId).Build());
        await AguardarEstadoAsync(m => m.GerandoOrcamento);
    }

    public async Task GerarOrcamentoAsync()
    {
        var validoAte = Ambiente.Relogio.GetUtcNow().AddHours(72);
        await PublicarAsync(new OrcamentoGeradoBuilder().DaOrdem(OrdemId).ComOrcamento(OrcamentoId).ValidoAte(validoAte).Build());
        await AguardarEstadoAsync(m => m.AguardandoAprovacao);
    }

    public async Task AprovarAsync()
    {
        await PublicarAsync(new ClienteAprovouOrcamentoBuilder().DaOrdem(OrdemId).Build());
        await AguardarEstadoAsync(m => m.AguardandoPagamento);
    }

    public Task SolicitarPagamentoAsync() =>
        PublicarAsync(new PagamentoSolicitadoBuilder().DaOrdem(OrdemId).ComPagamento(PagamentoId).Build());

    public async Task PagarAsync()
    {
        await PublicarAsync(new PagamentoAprovadoBuilder().DaOrdem(OrdemId).ComPagamento(PagamentoId).Build());
        await AguardarEstadoAsync(m => m.EmExecucao);
    }

    // Dispara o prazo de pagamento vigente, como o sweeper faria.
    public async Task ExpirarPagamentoAsync()
    {
        var saga = Saga();
        await PublicarAsync(new PrazoExpiradoBuilder().DaOrdem(OrdemId).DoTipo(TipoPrazo.Pagamento).ComPrazo(saga.PrazoPagamentoId!.Value).Build());
        await AguardarEstadoAsync(m => m.CancelandoPagamento);
    }

    public async Task InviabilizarExecucaoAsync()
    {
        await PublicarAsync(new ExecucaoInviavelBuilder().DaOrdem(OrdemId).Build());
        await AguardarEstadoAsync(m => m.Estornando);
    }

    public async Task FalharEstornoAsync()
    {
        await PublicarAsync(new EstornoFalhouBuilder().DaOrdem(OrdemId).ComPagamento(PagamentoId).Build());
        await AguardarEstadoAsync(m => m.EstornoPendenteManual);
    }

    public async Task CancelarPeloStaffAsync(string? motivo = null)
    {
        var builder = new CancelamentoSolicitadoBuilder().DaOrdem(OrdemId);
        await PublicarAsync(motivo is null ? builder.Build() : builder.ComMotivo(motivo).Build());
    }

    // Publica a mensagem com o MessageId do contrato como id do envelope (e o que o inbox usa) e espera a saga consumi-la,
    // com sucesso ou nao: depois disso o estado da saga e as mensagens enviadas ja refletem o processamento.
    public async Task PublicarAsync<T>(T mensagem) where T : class
    {
        var messageId = MessageIdDoContrato<T>.Obter(mensagem) ?? throw new InvalidOperationException($"{typeof(T).Name} nao tem MessageId.");

        await Ambiente.Harness.Bus.Publish(mensagem, Pipe.Execute<PublishContext<T>>(c => c.MessageId = messageId));

        (await Ambiente.Saga.Consumed.Any<T>(x => x.Context.MessageId == messageId))
            .Should().BeTrue($"a saga deveria ter consumido {typeof(T).Name}");

        Ambiente.Saga.Consumed.Select<T>(x => x.Context.MessageId == messageId, SemEspera).Single().Exception
            .Should().BeNull($"o consumo de {typeof(T).Name} nao pode falhar: evento sem transicao e ignorado, nunca vai para _error");
    }

    // Publica o evento e afirma que NADA mudou: nem a saga, nem a OS, nem o estoque, nem as mensagens enviadas. O relogio avanca
    // antes, entao qualquer activity que rodasse mudaria AtualizadaEm e o retrato.
    public async Task PublicarIgnoradoAsync<T>(T mensagem) where T : class
    {
        Ambiente.Relogio.Advance(TimeSpan.FromMinutes(1));
        var antes = await RetratoAsync();

        await PublicarAsync(mensagem);

        (await RetratoAsync()).Should().Be(antes, $"{typeof(T).Name} nao tem transicao neste estado e deve ser ignorado");
    }

    public sealed record Retrato(
        string Estado, DateTimeOffset AtualizadaEm, Guid? OrcamentoId, Guid? PagamentoId, Guid? PrazoAprovacaoId, Guid? PrazoPagamentoId,
        bool PorStaff, string? Motivo, StatusOrdemDeServico StatusDaOs, bool EstoqueReservado, int EstoqueDaPeca, int HistoricoDaOs,
        int Comandos, int Prazos, int Emails);

    public async Task<Retrato> RetratoAsync()
    {
        var saga = Saga();
        var ordem = await OrdemAsync();
        return new Retrato(
            saga.CurrentState, saga.AtualizadaEm, saga.OrcamentoId, saga.PagamentoId, saga.PrazoAprovacaoId, saga.PrazoPagamentoId,
            saga.CancelamentoPorStaff, saga.MotivoCancelamento, ordem.Status, ordem.EstoqueReservado, (await PecaAsync()).QuantidadeEstoque,
            ordem.HistoricoStatus.Count, TodosOsComandosEnviados().Count, (await PrazosAsync()).Count, Ambiente.Emails.Enviados.Count);
    }

    // Fault<T> de um comando que a saga enviou: o destino esgotou o retry e publicou a falha.
    public async Task PublicarFalhaAsync<TComando>(string erro = "Timeout simulado no destino.") where TComando : class
    {
        var comando = EnviadosDoTipo<TComando>().Last();
        var falha = new FaultEvent<TComando>(
            comando,
            MessageIdDoContrato<TComando>.Obter(comando),
            HostMetadataCache.Host,
            new TimeoutException(erro),
            Array.Empty<string>());
        var faultId = falha.FaultId;

        await Ambiente.Harness.Bus.Publish<Fault<TComando>>(falha, Pipe.Execute<PublishContext<Fault<TComando>>>(c => c.MessageId = faultId));

        (await Ambiente.Saga.Consumed.Any<Fault<TComando>>(x => x.Context.MessageId == faultId))
            .Should().BeTrue($"a saga deveria ter consumido Fault<{typeof(TComando).Name}>");
    }

    public async Task AguardarEstadoAsync(Func<OrdemDeServicoStateMachine, State> estado)
    {
        var correlacao = await Ambiente.Saga.Exists(OrdemId, estado, TimeSpan.FromSeconds(10));
        correlacao.Should().NotBeNull($"a saga deveria chegar ao estado esperado (esta em '{SagaOuNula()?.CurrentState}')");
    }

    // Retrato da saga no repositorio em memoria do harness.
    public OrdemDeServicoSagaState Saga() =>
        SagaOuNula() ?? throw new InvalidOperationException("A saga nao existe.");

    private OrdemDeServicoSagaState? SagaOuNula() =>
        Ambiente.Saga.Sagas.Contains(OrdemId);

    public Task<OrdemDeServico> OrdemAsync() =>
        Ambiente.ConsultarAsync(db => db.OrdensDeServico
            .Include(o => o.HistoricoStatus).Include(o => o.ItensPeca).Include(o => o.ItensServico)
            .AsNoTracking().SingleAsync(o => o.Id == OrdemId));

    public Task<Peca> PecaAsync() => Ambiente.ConsultarAsync(db => db.Pecas.AsNoTracking().SingleAsync(p => p.Id == PecaId));

    public Task<List<SagaPrazo>> PrazosAsync() =>
        Ambiente.ConsultarAsync(db => db.SagaPrazos.AsNoTracking().Where(p => p.CorrelationId == OrdemId).OrderBy(p => p.ExpiraEm).ToListAsync());

    // Os comandos que a saga enviou (Send) desta OS, na ordem. Vale depois de a saga consumir o evento que os gerou.
    public List<T> EnviadosDoTipo<T>() where T : class =>
        Ambiente.Harness.Sent.Select<T>(SemEspera).Select(x => x.Context.Message).ToList();

    // O MessageId do contrato e o do envelope (o que o inbox do destino usa) de cada comando enviado do tipo T.
    public List<(T Comando, Guid? IdDoEnvelope)> EnviadosComEnvelope<T>() where T : class =>
        Ambiente.Harness.Sent.Select<T>(SemEspera).Select(x => (x.Context.Message, x.Context.MessageId)).ToList();

    public int QuantidadeDeSagas() => Ambiente.Saga.Sagas.Select(_ => true, SemEspera).Count();

    // Todos os comandos enviados pela saga, na ordem do envio, e o nome da fila de destino de cada um.
    public List<object> TodosOsComandosEnviados() =>
        Ambiente.Harness.Sent.Select(_ => true, SemEspera).Select(x => x.MessageObject!).ToList();

    public Dictionary<string, string> EnderecosDosComandos() =>
        Ambiente.Harness.Sent.Select(_ => true, SemEspera).ToDictionary(x => x.MessageType.Name, x => x.Context.DestinationAddress!.AbsolutePath.TrimStart('/'));

    public ValueTask DisposeAsync() => Ambiente.DisposeAsync();
}
