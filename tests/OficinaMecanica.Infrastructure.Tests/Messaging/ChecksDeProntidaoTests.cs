using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OficinaMecanica.Application;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Messaging;

// Sem Docker: as dependencias "paradas" sao enderecos onde ninguem escuta (localhost:1).
public class ChecksDeProntidaoTests
{
    private static readonly HealthCheckContext Contexto = new()
    {
        Registration = new HealthCheckRegistration("teste", _ => null!, null, null)
    };

    [Fact]
    public async Task RabbitMqHealthCheck_ComBrokerInalcancavel_DeveResponderDegradedSemExporOHost()
    {
        var opcoes = Options.Create(new OpcoesDoRabbitMq { Host = "127.0.0.1", Port = 1 });
        var check = new RabbitMqHealthCheck(opcoes);

        var resultado = await check.CheckHealthAsync(Contexto);

        resultado.Status.Should().Be(HealthStatus.Degraded, "o outbox segura as mensagens, entao o broker fora nao tira o pod de rotacao");
        resultado.Description.Should().NotContain("127.0.0.1");
        resultado.Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task RabbitMqHealthCheck_ComCancelamentoDoChamador_DevePropagarOCancelamento()
    {
        var check = new RabbitMqHealthCheck(Options.Create(new OpcoesDoRabbitMq { Host = "127.0.0.1", Port = 1 }));
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();

        var act = () => check.CheckHealthAsync(Contexto, cancelamento.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SqlServerHealthCheck_ComBancoInalcancavel_DeveResponderUnhealthyDentroDoLimite()
    {
        var configuracao = ConfiguracaoDeTeste.Criar();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new AmbienteFalso("Testing"));
        services.AddApplication();
        services.AddInfrastructure(configuracao);
        await using var provedor = services.BuildServiceProvider(validateScopes: true);
        await using var escopo = provedor.CreateAsyncScope();
        var check = new SqlServerHealthCheck(escopo.ServiceProvider.GetRequiredService<AppDbContext>());
        var contexto = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("sqlserver", check, null, null)
        };

        var inicio = DateTime.UtcNow;
        var resultado = await check.CheckHealthAsync(contexto);

        resultado.Status.Should().Be(HealthStatus.Unhealthy, "sem o banco a API nao aceita escrita");
        (DateTime.UtcNow - inicio).Should().BeLessThan(TimeSpan.FromSeconds(10), "a probe nao pode ficar esperando o retry do EF");
    }
}
