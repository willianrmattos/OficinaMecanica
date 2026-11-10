using System.Security.Cryptography;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Integration.Tests.Fakes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace OficinaMecanica.Integration.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Chave RSA fixa so pra assinar/validar tokens de teste, sem depender do JWKS real do Seguranca via rede.
    // Uma instancia por factory (uma por classe de teste, via IClassFixture) - nao um campo static
    // compartilhado entre classes. RSA/RSACng nao e thread-safe pra assinar/validar concorrentemente,
    // e o xunit roda classes de teste diferentes em paralelo por padrao (cada uma com sua propria
    // instancia dessa factory rodando ao mesmo tempo). Um RSA estatico unico compartilhado entre todas
    // as classes causava falha intermitente de verdade (nao so teorica): reproduzi tanto
    // SecurityTokenInvalidSignatureException (401 em "Expected Created, but found Unauthorized") quanto,
    // sob concorrencia mais pesada, ObjectDisposedException no SafeBCryptKeyHandle nativo, so por causa
    // do compartilhamento - eliminado ao dar uma chave propria (e descartada junto) pra cada factory.
    public RSA TestSigningKey { get; } = RSA.Create(2048);
    public const string TestKid = "test-kid";

    // O publicador real (outbox do MassTransit) so entra junto com a saga: aqui as mensagens de integracao
    // ficam capturadas para os testes verificarem o que cada endpoint publicou. Uma instancia por factory,
    // pelo mesmo motivo da chave RSA: as classes de teste rodam em paralelo.
    public FakeIntegrationEventPublisher Publicador { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dbName = "TestDb_" + Guid.NewGuid();

        // Estes testes rodam sem RabbitMQ e sobre EF InMemory, que nao suporta o outbox transacional: troco o
        // transporte por memoria e a persistencia por nenhuma. Tem de ser UseSetting (e nao
        // ConfigureAppConfiguration), porque o Program le a configuracao antes de este factory acrescentar fontes.
        builder.UseSetting($"{OpcoesDaMensageria.Secao}:{nameof(OpcoesDaMensageria.Transporte)}", nameof(TransporteDaMensageria.EmMemoria));
        builder.UseSetting($"{OpcoesDaMensageria.Secao}:{nameof(OpcoesDaMensageria.Persistencia)}", nameof(PersistenciaDaMensageria.Nenhuma));

        // A saga sobe junto (repositorio em memoria, por causa da persistencia Nenhuma), mas o sweeper de prazos nao tem o que
        // varrer aqui: o publicador e um fake e nenhuma mensagem chega ao bus.
        builder.UseSetting($"{SagaOptions.Secao}:{nameof(SagaOptions.SweeperHabilitado)}", "false");

        builder.ConfigureServices(services =>
        {
            // No EF Core 9 o AddDbContext acumula as configuracoes em IDbContextOptionsConfiguration<T>: sem
            // remover tambem esta, o UseSqlServer do AddInfrastructure continuaria valendo ao lado do InMemory.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            var emailServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));

            if (emailServiceDescriptor != null)
                services.Remove(emailServiceDescriptor);

            services.AddScoped<IEmailService, FakeEmailService>();

            services.AddSingleton<IIntegrationEventPublisher>(Publicador);

            // Sobrescreve so a resolucao da chave de assinatura (pra chave de teste fixa acima),
            // depois da configuracao real do AddJwtBearer - Issuer/Audience continuam vindo do appsettings.json real
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var testKey = new RsaSecurityKey(TestSigningKey) { KeyId = TestKid };
                options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) => new[] { testKey };
            });
        });

        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            TestSigningKey.Dispose();
    }

    private class FakeEmailService : IEmailService
    {
        public Task EnviarAsync(string destinatario, string assunto, string corpo, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
