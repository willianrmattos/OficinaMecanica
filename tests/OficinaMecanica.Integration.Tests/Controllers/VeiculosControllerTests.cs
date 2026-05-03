using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.ValueObjects;
using OficinaMecanica.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class VeiculosControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public VeiculosControllerTests(CustomWebApplicationFactory factory)
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

    private async Task<Guid> SeedClienteAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cliente = new Cliente("Cliente Seed", Documento.Criar("52998224725"), null, null);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return cliente.Id;
    }

    [Fact]
    public async Task Criar_SemAutenticacao_DeveRetornar401()
    {
        var response = await _client.PostAsJsonAsync("/api/veiculos",
            new { Placa = "ABC1234", Marca = "Toyota", Modelo = "Corolla", Ano = 2020, ClienteId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_DeveRetornar201()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var clienteId = await SeedClienteAsync();

        var response = await _client.PostAsJsonAsync("/api/veiculos",
            new { Placa = "XYZ9876", Marca = "Honda", Modelo = "Civic", Ano = 2021, ClienteId = clienteId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Honda");
    }

    [Fact]
    public async Task ListarPorCliente_DeveRetornar200()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/veiculos/cliente/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ObterPorId_NaoExistente_DeveRetornar404()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/veiculos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
