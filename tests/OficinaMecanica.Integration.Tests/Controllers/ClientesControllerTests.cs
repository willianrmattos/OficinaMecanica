using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using System.Text.Json;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class ClientesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ClientesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> ObterTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Usuario = "admin", Senha = "Admin@123" });
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        return json.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Criar_SemAutenticacao_DeveRetornar401()
    {
        var response = await _client.PostAsJsonAsync("/api/clientes",
            new { Nome = "Teste", Documento = "52998224725" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_DeveRetornar201()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/clientes",
            new { Nome = "João Silva", Documento = "52998224725", Email = "joao@email.com", Telefone = "11999999999" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("João Silva");
    }

    [Fact]
    public async Task Listar_DeveRetornar200()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/clientes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
