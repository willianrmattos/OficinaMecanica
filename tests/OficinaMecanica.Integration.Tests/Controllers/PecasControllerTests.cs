using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class PecasControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public PecasControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
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

    private async Task<Guid> SeedPecaAsync(string nome = "Peca Seed")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var peca = new Peca(nome, null, null, 10m, 5, 2);
        db.Pecas.Add(peca);
        await db.SaveChangesAsync();
        return peca.Id;
    }

    [Fact]
    public async Task Criar_SemAutenticacao_DeveRetornar401()
    {
        var response = await _client.PostAsJsonAsync("/api/pecas",
            new { Nome = "Filtro de Ar", PrecoUnitario = 25, QuantidadeEstoque = 10, EstoqueMinimo = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_DeveRetornar201()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/pecas",
            new { Nome = "Filtro de Ar", PrecoUnitario = 25m, QuantidadeEstoque = 10, EstoqueMinimo = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Filtro de Ar");
    }

    [Fact]
    public async Task Listar_DeveRetornar200()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/pecas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ObterPorId_NaoExistente_DeveRetornar404()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/pecas/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdicionarEstoque_PecaExistente_DeveRetornarEstoqueAtualizado()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var pecaId = await SeedPecaAsync("Vela de Ignição");

        var estoqueResponse = await _client.PatchAsJsonAsync($"/api/pecas/{pecaId}/estoque", new { Quantidade = 10 });

        estoqueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var estoqueContent = await estoqueResponse.Content.ReadAsStringAsync();
        var estoqueJson = JsonDocument.Parse(estoqueContent);
        estoqueJson.RootElement.GetProperty("quantidadeEstoque").GetInt32().Should().Be(15);
    }

    [Fact]
    public async Task Desativar_PecaExistente_DeveRetornar204()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var pecaId = await SeedPecaAsync("Correia Dentada");

        var desativarResponse = await _client.PatchAsync($"/api/pecas/{pecaId}/desativar", null);

        desativarResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
