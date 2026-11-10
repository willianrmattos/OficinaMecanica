using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OficinaMecanica.Application;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Messaging;

// Sem Docker: as dependencias "paradas" sao enderecos onde ninguem escuta (127.0.0.1:1, conexao recusada na hora).
public class ChecksDeProntidaoTests
{
    private static readonly HealthCheckContext Contexto = new()
    {
        Registration = new HealthCheckRegistration("teste", _ => null!, null, null)
    };

    private static RabbitMqHealthCheck RabbitInalcancavel() =>
        new(Microsoft.Extensions.Options.Options.Create(new OpcoesDoRabbitMq { Host = "127.0.0.1", Port = 1, Username = "svc", Password = "x" }));

    [Fact]
    public async Task RabbitMqHealthCheck_ComBrokerInalcancavel_DeveResponderDegradedSemExporOHost()
    {
        var resultado = await RabbitInalcancavel().CheckHealthAsync(Contexto);

        resultado.Status.Should().Be(HealthStatus.Degraded, "o outbox segura as mensagens, então o broker fora não tira o pod de rotação");
        resultado.Description.Should().Be("Não foi possível conectar ao RabbitMQ; as mensagens ficam no outbox até o broker voltar.");
        resultado.Description.Should().NotContain("127.0.0.1");
        resultado.Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task RabbitMqHealthCheck_ComCancelamentoDoChamador_DevePropagarOCancelamento()
    {
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();

        var act = () => RabbitInalcancavel().CheckHealthAsync(Contexto, cancelamento.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static ServiceProvider CriarProvedor(string conexao)
    {
        var configuracao = ConfiguracaoDeTeste.Criar(("ConnectionStrings:DefaultConnection", conexao));
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new AmbienteFalso("Testing"));
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuracao);
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task SqlServerHealthCheck_ComBancoInalcancavel_DeveResponderUnhealthyDentroDoLimite()
    {
        await using var provedor = CriarProvedor("Server=tcp:127.0.0.1,1;Database=OficinaMecanicaDb;Encrypt=False;Connect Timeout=30;");
        await using var escopo = provedor.CreateAsyncScope();
        var check = new SqlServerHealthCheck(escopo.ServiceProvider.GetRequiredService<AppDbContext>());

        var inicio = DateTime.UtcNow;
        var resultado = await check.CheckHealthAsync(Contexto);

        resultado.Status.Should().Be(HealthStatus.Unhealthy, "sem o banco a API não aceita escrita");
        resultado.Description.Should().Be("Não foi possível conectar ao banco de dados.");
        (DateTime.UtcNow - inicio).Should().BeLessThan(TimeSpan.FromSeconds(10), "a probe não pode esperar o retry do EF nem o timeout de 30 s da conexão");
    }

    [Fact]
    public async Task SqlServerHealthCheck_ComBancoDisponivel_DeveResponderHealthyComMensagemEmPortugues()
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"saude-{Guid.NewGuid():N}").Options;
        await using var db = new AppDbContext(opcoes, Mock.Of<IMediator>());

        var resultado = await new SqlServerHealthCheck(db).CheckHealthAsync(Contexto);

        resultado.Status.Should().Be(HealthStatus.Healthy);
        resultado.Description.Should().Be("Conexão com o banco de dados estabelecida.");
    }

    [Fact]
    public async Task SqlServerHealthCheck_ComCancelamentoDoChamador_DevePropagarOCancelamento()
    {
        await using var provedor = CriarProvedor("Server=tcp:127.0.0.1,1;Database=OficinaMecanicaDb;Encrypt=False;");
        await using var escopo = provedor.CreateAsyncScope();
        var check = new SqlServerHealthCheck(escopo.ServiceProvider.GetRequiredService<AppDbContext>());
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();

        var act = () => check.CheckHealthAsync(Contexto, cancelamento.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- Limites de tempo e falha inesperada ----

    // Um servidor que aceita a conexao TCP (o kernel completa o handshake sem ninguem chamar Accept) e nunca fala o protocolo:
    // e o caso em que so o limite de 3 s encerra a espera, em qualquer sistema operacional (um endereco que descarta pacotes
    // dependeria da rede de quem roda o teste).
    private sealed class ServidorMudo : IDisposable
    {
        private readonly TcpListener _servidor = new(IPAddress.Loopback, 0);

        public ServidorMudo() => _servidor.Start();

        public int Porta => ((IPEndPoint)_servidor.LocalEndpoint).Port;

        public void Dispose() => _servidor.Stop();
    }

    // Mede so a avaliacao do check: montar o provedor e o contexto fica fora do cronometro, porque a janela do teste e
    // estreita (2,5 s a 3,8 s) e a montagem nao faz parte do limite que a readiness enxerga.
    private static async Task<(HealthCheckResult Resultado, TimeSpan Duracao)> MedirAsync(Func<Task<HealthCheckResult>> avaliar)
    {
        var cronometro = Stopwatch.StartNew();
        var resultado = await avaliar();
        cronometro.Stop();
        return (resultado, cronometro.Elapsed);
    }

    // Acima de 2,5 s mostra que o check esperou o limite de 3 s (e nao um menor) e abaixo de 3,8 s que um limite maior (4 s,
    // 4,5 s, os 30 s da conexao) estouraria, com folga para a maquina carregada.
    private static void DeveTerDesistidoNoLimiteDeTresSegundos(TimeSpan duracao, string porque) =>
        duracao.Should().BeGreaterThan(TimeSpan.FromSeconds(2.5)).And.BeLessThan(TimeSpan.FromSeconds(3.8), porque);

    [Fact]
    public async Task RabbitMqHealthCheck_ComBrokerQueAceitaAConexaoMasNaoResponde_DeveDesistirNoLimiteDeTresSegundos()
    {
        using var servidor = new ServidorMudo();
        var check = new RabbitMqHealthCheck(Microsoft.Extensions.Options.Options.Create(
            new OpcoesDoRabbitMq { Host = "127.0.0.1", Port = (ushort)servidor.Porta, Username = "svc", Password = "x" }));

        var (resultado, duracao) = await MedirAsync(() => check.CheckHealthAsync(Contexto));

        resultado.Status.Should().Be(HealthStatus.Degraded);
        DeveTerDesistidoNoLimiteDeTresSegundos(duracao,
            "o limite é de 3 s e a readiness tem timeoutSeconds: 5; o cliente sozinho leva ~8 s para desistir de um broker mudo");
    }

    [Fact]
    public async Task SqlServerHealthCheck_ComBancoQueAceitaAConexaoMasNaoResponde_DeveDesistirNoLimiteDeTresSegundos()
    {
        using var servidor = new ServidorMudo();
        await using var provedor = CriarProvedor($"Server=tcp:127.0.0.1,{servidor.Porta};Database=OficinaMecanicaDb;Encrypt=False;Connect Timeout=30;");
        await using var escopo = provedor.CreateAsyncScope();
        var check = new SqlServerHealthCheck(escopo.ServiceProvider.GetRequiredService<AppDbContext>());

        var (resultado, duracao) = await MedirAsync(() => check.CheckHealthAsync(Contexto));

        resultado.Status.Should().Be(HealthStatus.Unhealthy);
        DeveTerDesistidoNoLimiteDeTresSegundos(duracao,
            "o limite é de 3 s e a readiness tem timeoutSeconds: 5; o timeout da conexão é de 30 s");
    }

    [Fact]
    public async Task RabbitMqHealthCheck_ComBrokerCongeladoECancelamentoDoChamador_DevePropagarOCancelamentoEmVezDeVirarDegraded()
    {
        using var servidor = new ServidorMudo();
        var check = new RabbitMqHealthCheck(Microsoft.Extensions.Options.Options.Create(
            new OpcoesDoRabbitMq { Host = "127.0.0.1", Port = (ushort)servidor.Porta, Username = "svc", Password = "x" }));
        using var chamador = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var act = () => check.CheckHealthAsync(Contexto, chamador.Token);

        await act.Should().ThrowAsync<OperationCanceledException>("quem cancelou foi a probe, não o broker");
    }

    // O EF engole a falha de conexao e devolve falso; este criador joga a excecao inesperada direto, o unico jeito de exercitar o catch.
    private sealed class CriadorQueFalhaAoConectar : IDatabaseCreator
    {
        public bool EnsureDeleted() => throw new NotSupportedException();
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public bool EnsureCreated() => throw new NotSupportedException();
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public bool CanConnect() => throw new InvalidOperationException("falha inesperada do driver");
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("falha inesperada do driver");
    }

    [Fact]
    public async Task SqlServerHealthCheck_ComExcecaoInesperadaNoDriver_DeveResponderUnhealthyComAExcecaoEnaoDegraded()
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"saude-{Guid.NewGuid():N}")
            .ReplaceService<IDatabaseCreator, CriadorQueFalhaAoConectar>()
            .Options;
        await using var db = new AppDbContext(opcoes, Mock.Of<IMediator>());

        var resultado = await new SqlServerHealthCheck(db).CheckHealthAsync(Contexto);

        resultado.Status.Should().Be(HealthStatus.Unhealthy, "sem o banco a API não aceita escrita");
        resultado.Description.Should().Be("Não foi possível conectar ao banco de dados.");
        resultado.Exception.Should().BeOfType<InvalidOperationException>();
    }
}
