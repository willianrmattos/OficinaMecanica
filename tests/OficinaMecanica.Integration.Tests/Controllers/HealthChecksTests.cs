using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class HealthChecksTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HealthChecksTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_SemToken_DeveResponder200()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_SemToken_ComBusSaudavel_DeveResponder200()
    {
        var response = await _factory.CreateClient().GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<RelatorioDeProntidao>();
        corpo!.Status.Should().Be("Healthy");
        corpo.Checks.Keys.Should().BeEquivalentTo("masstransit-bus", "sqlserver");
    }

    [Fact]
    public async Task HealthReady_ComDependenciaDegraded_DeveResponder200MostrandoODegradedNoCorpo()
    {
        var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHealthChecks().AddCheck(
                "broker-lento",
                () => HealthCheckResult.Degraded("RabbitMQ fora do ar"),
                tags: new[] { "ready" })));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "Degraded nao tira o pod de rotacao");
        var corpo = await response.Content.ReadFromJsonAsync<RelatorioDeProntidao>();
        corpo!.Status.Should().Be("Degraded");
        corpo.Checks["broker-lento"].Status.Should().Be("Degraded");
        corpo.Checks["broker-lento"].Descricao.Should().Be("RabbitMQ fora do ar");
    }

    [Fact]
    public async Task Health_ComDependenciaDeProntidaoFalhando_DeveContinuarSaudavelEReadyDeveFalhar()
    {
        var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHealthChecks().AddCheck(
                "broker-indisponivel",
                () => HealthCheckResult.Unhealthy("RabbitMQ fora do ar"),
                tags: new[] { "ready" })));
        var client = factory.CreateClient();

        var liveness = await client.GetAsync("/health");
        var readiness = await client.GetAsync("/health/ready");

        liveness.StatusCode.Should().Be(HttpStatusCode.OK, "uma queda do broker nao pode derrubar a probe de liveness");
        readiness.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private sealed record RelatorioDeProntidao(string Status, Dictionary<string, ChecagemDeProntidao> Checks);

    private sealed record ChecagemDeProntidao(string Status, string? Descricao);
}
