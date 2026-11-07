using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// Monta a composicao REAL da aplicacao (AddApplication + AddInfrastructure + AddMensageria) sobre um banco novo
// no SQL Server e um virtual host novo no RabbitMQ dos containers. So o e-mail e trocado por um registro, e os
// consumers entram pelos ganchos de AddMensageria, sob o mesmo retry e o mesmo outbox da producao.
public sealed class AmbienteDeInfra : IAsyncDisposable
{
    private readonly IHost _host;

    private AmbienteDeInfra(IHost host, string connectionString, string virtualHost, RegistroDeEmails emails, LoggerCapturador logs, RegistroDeConsumo consumo)
    {
        _host = host;
        ConnectionString = connectionString;
        VirtualHost = virtualHost;
        Emails = emails;
        Logs = logs;
        Consumo = consumo;
    }

    public IServiceProvider Servicos => _host.Services;
    public string ConnectionString { get; }
    public string VirtualHost { get; }
    public RegistroDeEmails Emails { get; }
    public LoggerCapturador Logs { get; }
    public RegistroDeConsumo Consumo { get; }

    public static async Task<AmbienteDeInfra> CriarAsync(
        InfraFixture infra,
        Action<IBusRegistrationConfigurator>? registrarConsumers = null,
        Action<IBusRegistrationContext, IReceiveConfigurator>? configurarEndpoints = null,
        Action<IServiceCollection>? ajustarServicos = null)
    {
        var connectionString = await infra.CriarBancoAsync();
        var virtualHost = await infra.CriarVirtualHostAsync();

        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["JwtSettings:Issuer"] = "OficinaMecanica.Seguranca",
            ["JwtSettings:Audience"] = "OficinaMecanica.Client",
            ["JwtSettings:JwksUri"] = "https://localhost/.well-known/jwks.json",
            ["RabbitMq:Host"] = infra.RabbitHost,
            ["RabbitMq:Port"] = infra.RabbitPort.ToString(),
            ["RabbitMq:VirtualHost"] = virtualHost,
            ["RabbitMq:Username"] = infra.RabbitUsuario,
            ["RabbitMq:Password"] = infra.RabbitSenha,
            // Retry e varredura do outbox rapidos: o teste espera a entrega em segundos, nao nos 500 ms a 60 s da producao.
            ["Mensageria:RetryIntervalosMs:0"] = "50",
            ["Mensageria:RetryIntervalosMs:1"] = "50",
            ["Mensageria:RetryIntervalosMs:2"] = "50",
            ["Mensageria:OutboxQueryDelayMs"] = "100"
        }).Build();

        var emails = new RegistroDeEmails();
        var logs = new LoggerCapturador();
        var consumo = new RegistroDeConsumo();

        // Staging (e nao Development/Testing) de proposito: o ValidadorDasOpcoesDaMensageria so aceita o transporte real e o
        // outbox do EF fora desses dois ambientes, entao a composicao testada aqui e a mesma de producao.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = "Staging" });
        builder.Services.AddLogging(l =>
        {
            l.ClearProviders();
            l.SetMinimumLevel(LogLevel.Warning);
            l.AddProvider(logs);
        });
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(configuracao);
        builder.Services.AddMensageria(configuracao, registrarConsumers, configurarEndpoints);
        builder.Services.RemoveAll<IEmailService>();
        builder.Services.AddSingleton<IEmailService>(emails);
        builder.Services.AddSingleton(consumo);
        ajustarServicos?.Invoke(builder.Services);

        var host = builder.Build();

        // O servico de entrega do outbox consulta as tabelas assim que o bus sobe: o schema precisa existir antes.
        using (var escopo = host.Services.CreateScope())
            await escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        await host.StartAsync();

        return new AmbienteDeInfra(host, connectionString, virtualHost, emails, logs, consumo);
    }

    public AsyncServiceScope CriarEscopo() => _host.Services.CreateAsyncScope();

    public async Task SemearAsync(params object[] entidades)
    {
        await using var escopo = CriarEscopo();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(entidades);
        await db.SaveChangesAsync();
    }

    public async Task<T> ConsultarAsync<T>(Func<AppDbContext, Task<T>> consulta)
    {
        await using var escopo = CriarEscopo();
        return await consulta(escopo.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
