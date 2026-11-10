using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using OficinaMecanica.Application.Commands.AprovarOrcamento;
using OficinaMecanica.Application.Commands.CriarOrdemDeServico;
using OficinaMecanica.Contracts;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// A saga REAL contra SQL Server e RabbitMQ de verdade (containers), com o outbox/inbox e o retry da producao. Billing e Execucao sao
// consumers de teste ligados as filas reais (Filas.Billing e Filas.Execucao) que respondem com os contratos reais. A OS nasce
// pelo command da Application (como na API) e as decisoes do cliente tambem; o relogio da saga e do sweeper e controlado.
public sealed class AmbienteDaSagaInfra : IAsyncDisposable
{
    public const int EstoqueInicial = 10;
    public const int QuantidadeDaPeca = 2;
    private static readonly TimeSpan LimiteDeEspera = TimeSpan.FromSeconds(40);

    private Guid _clienteId;
    private Guid _veiculoId;
    private Guid _servicoId;

    private AmbienteDaSagaInfra(InfraFixture fixture, AmbienteDeInfra infra, ComportamentoDosServicos servicos, FakeTimeProvider relogio)
    {
        Fixture = fixture;
        Infra = infra;
        Servicos = servicos;
        Relogio = relogio;
    }

    public InfraFixture Fixture { get; }
    public AmbienteDeInfra Infra { get; }
    public ComportamentoDosServicos Servicos { get; }
    public FakeTimeProvider Relogio { get; }

    public static async Task<AmbienteDaSagaInfra> CriarAsync(
        InfraFixture fixture,
        Action<IServiceCollection>? ajustarServicos = null,
        bool comEndpointDaSaga = true,
        params (string Chave, string? Valor)[] configuracao)
    {
        var comportamento = new ComportamentoDosServicos();
        var relogio = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var infra = await AmbienteDeInfra.CriarAsync(
            fixture,
            registrarConsumers: bus =>
            {
                if (comEndpointDaSaga)
                    bus.AddSagaDaOrdemDeServico();
                bus.AddConsumer<BillingSimulado>();
                bus.AddConsumer<ExecucaoSimulada>();
            },
            configurarEndpoints: (contexto, bus) =>
            {
                if (comEndpointDaSaga)
                    bus.ReceiveEndpointDaSaga(contexto);

                // Os "outros servicos" nao usam o outbox do OS Service: cada um teria o seu. Tambem ficam sem UseMessageRetry, entao
                // um consumer que lanca produz o Fault na primeira falha: isso prova Fault, fila _error e redrive nos testes ponta a
                // ponta, mas nao o retry do endpoint de destino, que e provado em RetryEFilaDeErroTests.
                bus.ReceiveEndpoint(Filas.Billing, endpoint => endpoint.ConfigureConsumer<BillingSimulado>(contexto));
                bus.ReceiveEndpoint(Filas.Execucao, endpoint => endpoint.ConfigureConsumer<ExecucaoSimulada>(contexto));
            },
            ajustarServicos: servicos =>
            {
                servicos.AddSingleton(comportamento);
                servicos.RemoveAll<TimeProvider>();
                servicos.AddSingleton<TimeProvider>(relogio);
                ajustarServicos?.Invoke(servicos);
            },
            configuracaoExtra: new (string, string?)[] { ("Saga:SweeperHabilitado", "false") }.Concat(configuracao),
            compor: (servicos, cfg) => servicos.AddServicosDaSaga(cfg));

        var ambiente = new AmbienteDaSagaInfra(fixture, infra, comportamento, relogio);
        await ambiente.SemearCadastrosAsync();
        return ambiente;
    }

    public IServiceProvider Provedor => Infra.Servicos;

    private async Task SemearCadastrosAsync()
    {
        var cliente = new ClienteBuilder().Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();
        var servico = new ServicoBuilder().ComNome("Troca de óleo").ComPreco(150m).Build();
        await Infra.SemearAsync(cliente, veiculo, servico);
        _clienteId = cliente.Id;
        _veiculoId = veiculo.Id;
        _servicoId = servico.Id;
    }

    // Cada OS tem a sua peca, para o estoque de uma nunca depender da outra quando varias saem em paralelo.
    public sealed record OrdemCriada(Guid OrdemId, Guid PecaId);

    public async Task<OrdemCriada> CriarOrdemAsync(int estoque = EstoqueInicial)
    {
        var peca = new PecaBuilder().ComNome($"Filtro {Guid.NewGuid():N}"[..14]).ComPreco(40m).ComEstoque(estoque).Build();
        await Infra.SemearAsync(peca);

        var ordem = await EnviarAsync(new CriarOrdemDeServicoCommand(
            _clienteId, _veiculoId, "Barulho ao frear.",
            new List<ItemServicoInput> { new(_servicoId, 1) },
            new List<ItemPecaInput> { new(peca.Id, QuantidadeDaPeca) }));

        return new OrdemCriada(ordem.Id, peca.Id);
    }

    public async Task<OrdemCriada> SemearOrdemSemSagaAsync()
    {
        var peca = new PecaBuilder().ComEstoque(EstoqueInicial).Build();
        var ordem = new OrdemDeServicoBuilder().DoCliente(_clienteId).DoVeiculo(_veiculoId).ComPeca(peca.Id, peca.Nome, peca.PrecoUnitario, QuantidadeDaPeca).Recebida();
        await Infra.SemearAsync(peca, ordem);
        return new OrdemCriada(ordem.Id, peca.Id);
    }

    public async Task<T> EnviarAsync<T>(IRequest<T> comando)
    {
        await using var escopo = Infra.CriarEscopo();
        return await escopo.ServiceProvider.GetRequiredService<IMediator>().Send(comando);
    }

    public Task AprovarAsync(Guid ordemId) => EnviarAsync(new AprovarOrcamentoCommand(ordemId));

    // Publica direto no broker, como o Billing ou a Execucao fariam (nao como o OS Service, que usa o outbox).
    public Task PublicarAsync<T>(T mensagem) where T : class
    {
        var messageId = MessageIdDoContrato<T>.Obter(mensagem);
        var bus = Provedor.GetRequiredService<IBus>();
        return messageId is { } id
            ? bus.Publish(mensagem, Pipe.Execute<PublishContext<T>>(c => c.MessageId = id))
            : bus.Publish(mensagem);
    }

    public Task<string?> EstadoDaSagaAsync(Guid ordemId) =>
        Infra.ConsultarAsync(db => db.Set<OrdemDeServicoSagaState>().Where(s => s.CorrelationId == ordemId).Select(s => s.CurrentState).SingleOrDefaultAsync());

    public Task<OrdemDeServicoSagaState> SagaAsync(Guid ordemId) =>
        Infra.ConsultarAsync(db => db.Set<OrdemDeServicoSagaState>().AsNoTracking().SingleAsync(s => s.CorrelationId == ordemId));

    public Task<OrdemDeServico> OrdemAsync(Guid ordemId) =>
        Infra.ConsultarAsync(db => db.OrdensDeServico.AsNoTracking().Include(o => o.HistoricoStatus).SingleAsync(o => o.Id == ordemId));

    public Task<int> EstoqueAsync(Guid pecaId) =>
        Infra.ConsultarAsync(db => db.Pecas.AsNoTracking().Where(p => p.Id == pecaId).Select(p => p.QuantidadeEstoque).SingleAsync());

    public async Task AguardarEstadoAsync(Guid ordemId, string estado, TimeSpan? limite = null)
    {
        var chegou = await Esperar.AteAsync(async () => await EstadoDaSagaAsync(ordemId) == estado, limite ?? LimiteDeEspera);
        chegou.Should().BeTrue($"a saga da OS {ordemId} deveria chegar a '{estado}' (esta em '{await EstadoDaSagaAsync(ordemId)}'; {await DescreverErrosAsync()})");
    }

    public async Task AguardarStatusDaOsAsync(Guid ordemId, Domain.Enums.StatusOrdemDeServico status)
    {
        var chegou = await Esperar.AteAsync(async () => (await OrdemAsync(ordemId)).Status == status, LimiteDeEspera);
        chegou.Should().BeTrue($"a OS deveria chegar a {status}");
    }

    // Mensagens que sobraram nas filas de erro: depois de qualquer cenario correto, todas devem estar vazias.
    public async Task<int> MensagensEmErroAsync()
    {
        var total = 0;
        foreach (var fila in new[] { Filas.Saga, Filas.Billing, Filas.Execucao })
            total += await Fixture.MensagensNaFilaAsync(Infra.VirtualHost, fila + "_error");
        return total;
    }

    private async Task<string> DescreverErrosAsync() =>
        $"mensagens em _error: {await MensagensEmErroAsync()}; logs de erro: {string.Join(" | ", Infra.Logs.Registros.Where(r => r.Nivel >= Microsoft.Extensions.Logging.LogLevel.Error).Select(r => r.Mensagem).Take(3))}";

    public async Task RodarSweeperAsync()
    {
        await using var escopo = Infra.CriarEscopo();
        await escopo.ServiceProvider.GetRequiredService<SweeperDePrazos>().ProcessarLoteAsync();
    }

    // O outbox so entrega ao broker depois do commit: espera o que ficou pendente ser entregue.
    public Task<bool> OutboxVaziaAsync() =>
        Esperar.AteAsync(async () => await Infra.ConsultarAsync(db => db.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>().CountAsync()) == 0, LimiteDeEspera);

    public ValueTask DisposeAsync() => Infra.DisposeAsync();
}
