using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class ServicosControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ServicosControllerTests(CustomWebApplicationFactory factory)
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

    private async Task<Guid> SeedServicoAsync(string nome = "Serviço Seed")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var servico = new Servico(nome, null, 80m, 30);
        db.Servicos.Add(servico);
        await db.SaveChangesAsync();
        return servico.Id;
    }

    [Fact]
    public async Task Criar_SemAutenticacao_DeveRetornar401()
    {
        var response = await _client.PostAsJsonAsync("/api/servicos",
            new { Nome = "Troca de Óleo", Preco = 80, TempoEstimadoMinutos = 30 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_DeveRetornar201()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/servicos",
            new { Nome = "Troca de Óleo", Preco = 80m, TempoEstimadoMinutos = 30 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Troca de Óleo");
    }

    [Fact]
    public async Task Listar_DeveRetornar200()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/servicos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ObterPorId_NaoExistente_DeveRetornar404()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/servicos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Desativar_ServicoExistente_DeveRetornar204()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var servicoId = await SeedServicoAsync("Balanceamento");

        var desativarResponse = await _client.PatchAsync($"/api/servicos/{servicoId}/desativar", null);

        desativarResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
