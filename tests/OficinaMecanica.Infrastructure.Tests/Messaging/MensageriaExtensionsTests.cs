using System.Data;
using FluentAssertions;
using MassTransit;
using MassTransit.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OficinaMecanica.Application;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Messaging;

public class MensageriaExtensionsTests
{
    private static ServiceProvider CriarProvedor(IConfiguration configuracao, Action<IServiceCollection>? ajustes = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new AmbienteFalso("Testing"));
        services.AddApplication();
        services.AddInfrastructure(configuracao);
        services.AddMensageria(configuracao);
        ajustes?.Invoke(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task AddMensageria_DeveRegistrarAPortaDePublicacaoSobreOMassTransit()
    {
        await using var provedor = CriarProvedor(ConfiguracaoDeTeste.Criar());
        await using var escopo = provedor.CreateAsyncScope();

        escopo.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().Should().BeOfType<MassTransitIntegrationEventPublisher>();
    }

    [Fact]
    public async Task AddMensageria_DeveEsperarOBusIniciarAntesDeLiberarOHost()
    {
        await using var provedor = CriarProvedor(ConfiguracaoDeTeste.Criar(("Mensageria:TimeoutDeInicioSegundos", "15")));

        var host = provedor.GetRequiredService<IOptions<MassTransitHostOptions>>().Value;

        host.WaitUntilStarted.Should().BeTrue();
        host.StartTimeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task AddMensageria_OutboxNaoDeveUsarIsolamentoSerializable()
    {
        await using var provedor = CriarProvedor(ConfiguracaoDeTeste.Criar(("Mensageria:Persistencia", "EntityFramework")));

        var outbox = provedor.GetRequiredService<IOptions<EntityFrameworkOutboxOptions<AppDbContext>>>().Value;

        // Serializable quebra o servico de entrega do bus outbox (READPAST so vale em READ COMMITTED e REPEATABLE READ).
        outbox.IsolationLevel.Should().NotBe(IsolationLevel.Serializable);
    }

    [Fact]
    public async Task AddMensageria_SemPersistencia_NaoDeveRegistrarOOutboxDoEf()
    {
        await using var provedor = CriarProvedor(ConfiguracaoDeTeste.Criar(("Mensageria:Persistencia", "Nenhuma")));

        provedor.GetServices<IConfigureOptions<EntityFrameworkOutboxOptions<AppDbContext>>>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddMensageria_ComTransporteEmMemoria_SoDeveTerOsChecksDoBusEDoBancoNaProntidade()
    {
        await using var provedor = CriarProvedor(ConfiguracaoDeTeste.Criar());

        var registros = provedor.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        registros.Select(r => r.Name).Should().BeEquivalentTo("masstransit-bus", "sqlserver");
        registros.Should().OnlyContain(r => r.Tags.Contains("ready"), "o /health do processo nao pode depender de nenhuma delas");
    }

    [Fact]
    public async Task AddMensageria_ComTransporteRabbitMq_DeveAcrescentarOCheckDeConexaoAmqpNaProntidade()
    {
        await using var provedor = CriarProvedor(ConfiguracaoDeTeste.Criar(("Mensageria:Transporte", "RabbitMq")));

        var registros = provedor.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        registros.Select(r => r.Name).Should().BeEquivalentTo("masstransit-bus", "sqlserver", "rabbitmq");
        registros.Should().OnlyContain(r => r.Tags.Contains("ready"));
    }

    [Fact]
    public async Task AddMensageria_BrokerForaNaoDeveTirarOPodDeRotacao_SoOBancoPodeFalharAProntidao()
    {
        await using var provedor = CriarProvedor(ConfiguracaoDeTeste.Criar(("Mensageria:Transporte", "RabbitMq")));

        var registros = provedor.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToDictionary(r => r.Name);

        registros["masstransit-bus"].FailureStatus.Should().Be(HealthStatus.Degraded);
        registros["sqlserver"].FailureStatus.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void OpcoesDaMensageria_SemConfiguracao_DeveUsarRabbitMqEOutboxDoEntityFramework()
    {
        var opcoes = new OpcoesDaMensageria();

        opcoes.Transporte.Should().Be(TransporteDaMensageria.RabbitMq);
        opcoes.Persistencia.Should().Be(PersistenciaDaMensageria.EntityFramework);
        opcoes.IntervalosDeRetry().Select(i => i.TotalMilliseconds).Should().Equal(500, 2_000, 10_000, 30_000, 60_000);
    }

    [Fact]
    public void OpcoesDaMensageria_IntervalosDoAppsettings_DevemSubstituirOsPadroesEmVezDeSomarSeAEles()
    {
        var configuracao = ConfiguracaoDeTeste.Criar(
            ("Mensageria:RetryIntervalosMs:0", "10"),
            ("Mensageria:RetryIntervalosMs:1", "20"));

        var opcoes = configuracao.GetSection(OpcoesDaMensageria.Secao).Get<OpcoesDaMensageria>()!;

        opcoes.IntervalosDeRetry().Select(i => i.TotalMilliseconds).Should().Equal(10, 20);
    }

    [Fact]
    public async Task OpcoesDoRabbitMq_DeveLerOsCincoCamposDaConfiguracao()
    {
        var configuracao = ConfiguracaoDeTeste.Criar(
            ("RabbitMq:Host", "broker.interno"),
            ("RabbitMq:Port", "5677"),
            ("RabbitMq:VirtualHost", "oficina"),
            ("RabbitMq:Username", "app"),
            ("RabbitMq:Password", "segredo"));
        await using var provedor = CriarProvedor(configuracao);

        var rabbit = provedor.GetRequiredService<IOptions<OpcoesDoRabbitMq>>().Value;

        rabbit.Should().BeEquivalentTo(new OpcoesDoRabbitMq
        {
            Host = "broker.interno",
            Port = 5677,
            VirtualHost = "oficina",
            Username = "app",
            Password = "segredo"
        });
    }

    [Fact]
    public async Task AddMensageria_ComTransporteRabbitMq_DeveMontarOBusSemConectar()
    {
        var chamadas = new List<string>();
        var configuracao = ConfiguracaoDeTeste.Criar(
            ("Mensageria:Transporte", "RabbitMq"),
            ("RabbitMq:Host", "host.que.nao.existe"),
            ("RabbitMq:Username", "app"),
            ("RabbitMq:Password", "segredo"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new AmbienteFalso("Testing"));
        services.AddApplication();
        services.AddInfrastructure(configuracao);
        services.AddMensageria(
            configuracao,
            registrarConsumers: _ => chamadas.Add("registrar"),
            configurarEndpoints: (_, _) => chamadas.Add("endpoints"));
        await using var provedor = services.BuildServiceProvider(validateScopes: true);

        var bus = provedor.GetRequiredService<IBusControl>();

        bus.Should().NotBeNull();
        chamadas.Should().Equal("registrar", "endpoints");
    }

    [Fact]
    public async Task AddInfrastructure_ForaDeUmConsumer_DeveHabilitarORetryDoEf()
    {
        await using var provedor = CriarProvedor(ConfiguracaoDeTeste.Criar());
        await using var escopo = provedor.CreateAsyncScope();

        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.CreateExecutionStrategy().Should().BeOfType<SqlServerRetryingExecutionStrategy>();
    }

    [Fact]
    public async Task AddInfrastructure_DentroDeUmConsumer_NaoDeveHabilitarORetryDoEf()
    {
        var configuracao = ConfiguracaoDeTeste.Criar();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new AmbienteFalso("Testing"));
        services.AddApplication();
        services.AddInfrastructure(configuracao);
        services.AddMensageria(
            configuracao,
            registrarConsumers: x => x.AddConsumer<ConsumerQueLeAEstrategiaDoEf>(),
            configurarEndpoints: (contexto, cfg) => cfg.ReceiveEndpoint("fila-de-teste", e => e.ConfigureConsumer<ConsumerQueLeAEstrategiaDoEf>(contexto)));
        services.AddMassTransitTestHarness();
        services.AddSingleton<EstrategiaObservada>();
        await using var provedor = services.BuildServiceProvider(validateScopes: true);
        var harness = provedor.GetRequiredService<MassTransit.Testing.ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new MensagemDeTeste(Guid.NewGuid(), Guid.NewGuid()));

        (await harness.Consumed.Any<MensagemDeTeste>()).Should().BeTrue();
        provedor.GetRequiredService<EstrategiaObservada>().Estrategia.Should().NotBeNull().And.NotBe(typeof(SqlServerRetryingExecutionStrategy));
    }

    private sealed class EstrategiaObservada
    {
        public Type? Estrategia { get; set; }
    }

    private sealed class ConsumerQueLeAEstrategiaDoEf : IConsumer<MensagemDeTeste>
    {
        private readonly AppDbContext _db;
        private readonly EstrategiaObservada _observada;

        public ConsumerQueLeAEstrategiaDoEf(AppDbContext db, EstrategiaObservada observada)
        {
            _db = db;
            _observada = observada;
        }

        public Task Consume(ConsumeContext<MensagemDeTeste> context)
        {
            _observada.Estrategia = _db.Database.CreateExecutionStrategy().GetType();
            return Task.CompletedTask;
        }
    }
}
