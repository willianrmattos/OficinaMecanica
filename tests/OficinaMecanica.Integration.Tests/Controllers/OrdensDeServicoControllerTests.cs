using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class OrdensDeServicoControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public OrdensDeServicoControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ObterPorNumero_Inexistente_DeveRetornar404()
    {
        var response = await _client.GetAsync("/api/ordens-de-servico/numero/OS-INEXISTENTE");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ObterTempoMedio_DeveRetornar200()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/ordens-de-servico/tempo-medio");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> ObterTokenAsync()
    {
        return await Task.FromResult(TestTokenFactory.GerarToken(_factory));
    }
}
