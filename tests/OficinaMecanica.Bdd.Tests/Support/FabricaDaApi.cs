using System.Security.Cryptography;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Bdd.Tests.Support;

// A API de verdade (controllers, handlers, saga, activities, EF InMemory) com tres trocas: o relogio e controlado, o
// transporte do bus e em memoria dentro do harness do MassTransit e o Billing e a Execucao sao consumers programaveis nas
// filas reais deles. Nada de Docker, de RabbitMQ nem de SQL Server.
public sealed class FabricaDaApi : WebApplicationFactory<Program>
{
    public const string KidDeTeste = "test-kid";
    public static readonly DateTimeOffset Inicio = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    // Uma chave por fabrica (e uma fabrica por cenario): o RSA nao e thread-safe e nao deve ser compartilhado.
    public RSA ChaveDeAssinatura { get; } = RSA.Create(2048);
    public FakeTimeProvider Relogio { get; } = new(Inicio);
    public RoteiroDosServicos Roteiro { get; } = new();
    public RegistroDeEmails Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var banco = "bdd_" + Guid.NewGuid();

        // O EF InMemory nao suporta o outbox transacional: transporte em memoria e persistencia nenhuma. Tem de ser UseSetting
        // (e nao ConfigureAppConfiguration), porque o Program le a configuracao antes de esta fabrica acrescentar fontes.
        builder.UseSetting($"{OpcoesDaMensageria.Secao}:{nameof(OpcoesDaMensageria.Transporte)}", nameof(TransporteDaMensageria.EmMemoria));
        builder.UseSetting($"{OpcoesDaMensageria.Secao}:{nameof(OpcoesDaMensageria.Persistencia)}", nameof(PersistenciaDaMensageria.Nenhuma));

        // O sweeper nao roda sozinho: o cenario avanca o relogio e o dispara a mao.
        builder.UseSetting($"{SagaOptions.Secao}:{nameof(SagaOptions.SweeperHabilitado)}", "false");

        // Retry curto: uma falha tecnica esgota as tentativas em milissegundos em vez dos ~100 s de producao.
        builder.UseSetting($"{OpcoesDaMensageria.Secao}:RetryIntervalosMs:0", "20");
        builder.UseSetting($"{OpcoesDaMensageria.Secao}:RetryIntervalosMs:1", "20");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opcoes => opcoes.UseInMemoryDatabase(banco));

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Relogio);
            services.AddSingleton(Relogio);

            services.AddSingleton(Roteiro);

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opcoes =>
            {
                var chave = new RsaSecurityKey(ChaveDeAssinatura) { KeyId = KidDeTeste };
                opcoes.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) => new[] { chave };
            });

            // O harness SUBSTITUI o bus e o repositorio de saga que o AddMensageria registrou: por isso a saga e o endpoint
            // os-saga (com o mesmo retry da API) sao declarados de novo aqui, e o Billing e a Execucao entram como consumers
            // nas filas que a saga usa como destino dos comandos.
            services.AddMassTransitTestHarness(bus =>
            {
                bus.AddSagaDaOrdemDeServico(repositorioEmMemoria: true);
                bus.AddConsumer<BillingSimulado>();
                bus.AddConsumer<ExecucaoSimulada>();

                bus.UsingInMemory((contexto, endpoints) =>
                {
                    endpoints.ReceiveEndpointDaSaga(contexto);
                    endpoints.ReceiveEndpoint(Filas.Billing, e => e.ConfigureConsumer<BillingSimulado>(contexto));
                    endpoints.ReceiveEndpoint(Filas.Execucao, e => e.ConfigureConsumer<ExecucaoSimulada>(contexto));
                });
            });
        });

        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            ChaveDeAssinatura.Dispose();
    }
}
