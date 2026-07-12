using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class RecusarOrcamentoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RecusarOrcamentoTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RecusarOrcamento_OrdemInexistente_DeveRetornar400()
    {
        var response = await _client.PostAsJsonAsync($"/api/ordens-de-servico/{Guid.NewGuid()}/recusar", new { Motivo = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
