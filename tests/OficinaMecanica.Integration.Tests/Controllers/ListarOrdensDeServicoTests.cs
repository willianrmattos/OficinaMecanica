using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class ListarOrdensDeServicoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ListarOrdensDeServicoTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task Listar_SemFiltroStatus_DeveExcluirFinalizadasEEntreguesEOrdenarPorPrioridade()
    {
        var recebidaAntiga = new OrdemDeServicoBuilder().Recebida();
        var recebidaNova = new OrdemDeServicoBuilder().Recebida();
        var emExecucao = new OrdemDeServicoBuilder().EmExecucao();
        var finalizada = new OrdemDeServicoBuilder().Finalizada();
        var entregue = new OrdemDeServicoBuilder().Entregue();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.OrdensDeServico.AddRange(recebidaAntiga, recebidaNova, emExecucao, finalizada, entregue);
            await db.SaveChangesAsync();

            db.Entry(recebidaAntiga).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-5);
            db.Entry(recebidaNova).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-1);
            db.Entry(emExecucao).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-3);
            await db.SaveChangesAsync();
        }

        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/ordens-de-servico?pagina=1&tamanhoPagina=50");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var itens = json.RootElement.GetProperty("itens").EnumerateArray().ToList();

        var statusPorId = itens.ToDictionary(
            i => i.GetProperty("id").GetGuid(),
            i => i.GetProperty("status").GetString());

        // Nenhuma OS Finalizada ou Entregue deve aparecer na listagem padrão (sem filtroStatus).
        statusPorId.Values.Should().NotContain("Finalizada");
        statusPorId.Values.Should().NotContain("Entregue");
        statusPorId.Should().NotContainKey(finalizada.Id);
        statusPorId.Should().NotContainKey(entregue.Id);

        // As demais devem aparecer.
        statusPorId.Should().ContainKey(recebidaAntiga.Id);
        statusPorId.Should().ContainKey(recebidaNova.Id);
        statusPorId.Should().ContainKey(emExecucao.Id);

        var ids = itens.Select(i => i.GetProperty("id").GetGuid()).ToList();
        var indiceEmExecucao = ids.IndexOf(emExecucao.Id);
        var indiceRecebidaAntiga = ids.IndexOf(recebidaAntiga.Id);
        var indiceRecebidaNova = ids.IndexOf(recebidaNova.Id);

        // EmExecucao tem prioridade maior que Recebida, deve vir antes.
        indiceEmExecucao.Should().BeLessThan(indiceRecebidaAntiga);
        indiceEmExecucao.Should().BeLessThan(indiceRecebidaNova);

        // Dentro do mesmo status (Recebida), a mais antiga vem primeiro.
        indiceRecebidaAntiga.Should().BeLessThan(indiceRecebidaNova);
    }

    [Fact]
    public async Task Listar_ComFiltroStatusFinalizada_DeveRetornarFinalizadas()
    {
        var finalizada = new OrdemDeServicoBuilder().Finalizada();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OrdensDeServico.Add(finalizada);
            await db.SaveChangesAsync();
        }

        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/ordens-de-servico?pagina=1&tamanhoPagina=50&filtroStatus=Finalizada");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var itens = json.RootElement.GetProperty("itens").EnumerateArray().ToList();

        var ids = itens.Select(i => i.GetProperty("id").GetGuid()).ToList();

        // Filtro explícito sobrepõe a exclusão padrão: a OS finalizada deve aparecer.
        ids.Should().Contain(finalizada.Id);
        itens.Should().OnlyContain(i => i.GetProperty("status").GetString() == "Finalizada");
    }
}
