using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using OficinaMecanica.Infrastructure.Tests.Support;
using Testcontainers.RabbitMq;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// A API real (Program, Development, SQL Server e RabbitMQ de verdade) com as dependencias caindo no meio do teste:
// broker fora deixa /health/ready em 200 com o rabbitmq Degraded (o outbox segura as mensagens), banco fora derruba
// /health/ready para 503, e /health (liveness) nunca depende de nenhuma delas.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class SaudeDoBrokerTests
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(40);

    private readonly InfraFixture _infra;

    public SaudeDoBrokerTests(InfraFixture infra) => _infra = infra;

    private static async Task<(HttpStatusCode Status, string Agregado, Dictionary<string, string> Checks)> LerProntidaoAsync(HttpClient client)
    {
        var resposta = await client.GetAsync("/health/ready");
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var checks = json.RootElement.GetProperty("checks").EnumerateObject()
            .ToDictionary(c => c.Name, c => c.Value.GetProperty("status").GetString()!);
        return (resposta.StatusCode, json.RootElement.GetProperty("status").GetString()!, checks);
    }

    [Fact]
    public async Task Prontidao_ComBrokerParadoFicaDegradedEmBancoForaFicaNaoSaudavel_ELivenessNaoMuda()
    {
        var usuario = "oficina";
        var senha = Guid.NewGuid().ToString("N");
        await using var rabbit = new RabbitMqBuilder(ImagensDeInfra.RabbitMq)
            .WithName($"os-f3b-rabbit-saude-{Guid.NewGuid().ToString("N")[..8]}")
            .WithUsername(usuario)
            .WithPassword(senha)
            .Build();
        await rabbit.StartAsync();
        var connectionString = await _infra.CriarBancoAsync();

        using var variaveis = new VariaveisDeAmbiente(new Dictionary<string, string>
        {
            ["ConnectionStrings__DefaultConnection"] = connectionString,
            ["RabbitMq__Host"] = rabbit.Hostname,
            ["RabbitMq__Port"] = rabbit.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq__VirtualHost"] = "/",
            ["RabbitMq__Username"] = usuario,
            ["RabbitMq__Password"] = senha
        });
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var saudavel = await LerProntidaoAsync(client);
        saudavel.Status.Should().Be(HttpStatusCode.OK);
        saudavel.Agregado.Should().Be("Healthy");
        saudavel.Checks.Should().Contain(new KeyValuePair<string, string>("rabbitmq", "Healthy")).And.Contain(new KeyValuePair<string, string>("sqlserver", "Healthy"));

        // Broker parado: 200 com o rabbitmq Degraded.
        await rabbit.StopAsync();
        (await Esperar.AteAsync(async () => (await LerProntidaoAsync(client)).Checks["rabbitmq"] == "Degraded", Limite))
            .Should().BeTrue("o check abre uma conexao AMQP de verdade e nao a consegue com o broker parado");
        var semBroker = await LerProntidaoAsync(client);
        semBroker.Status.Should().Be(HttpStatusCode.OK, "o outbox segura as mensagens: a API continua aceitando escrita");
        semBroker.Agregado.Should().Be("Degraded");
        semBroker.Checks["sqlserver"].Should().Be("Healthy");
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Banco fora (banco do teste posto offline; o SQL Server do fixture segue de pe para os outros testes): 503.
        await ColocarBancoOfflineAsync(connectionString);
        (await Esperar.AteAsync(async () => (await LerProntidaoAsync(client)).Status == HttpStatusCode.ServiceUnavailable, Limite))
            .Should().BeTrue("sem o banco a API nao aceita escrita");
        var semBanco = await LerProntidaoAsync(client);
        semBanco.Agregado.Should().Be("Unhealthy");
        semBanco.Checks["sqlserver"].Should().Be("Unhealthy");
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK, "a liveness nao depende de nenhuma dependencia");
    }

    private static async Task ColocarBancoOfflineAsync(string connectionStringDoBanco)
    {
        var builder = new SqlConnectionStringBuilder(connectionStringDoBanco);
        var banco = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        await using var conexao = new SqlConnection(builder.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = $"ALTER DATABASE [{banco}] SET OFFLINE WITH ROLLBACK IMMEDIATE";
        await comando.ExecuteNonQueryAsync();
    }
}
