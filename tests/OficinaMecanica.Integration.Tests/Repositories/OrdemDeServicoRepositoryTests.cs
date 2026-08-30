using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Integration.Tests.Repositories;

public class OrdemDeServicoRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrdemDeServicoRepositoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ObterTempoMedioPorStatusAsync_CalculaDuracaoEntreMudancasDeStatusConsecutivas()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repositorio = scope.ServiceProvider.GetRequiredService<IOrdemDeServicoRepository>();

        var ordemId = Guid.NewGuid();
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        // Recebida por 2h, EmDiagnostico por 1h, AguardandoAprovacao ainda em aberto
        // (sem par seguinte - nao entra no calculo, ainda nao "saiu" desse status).
        AdicionarHistorico(context, ordemId, StatusOrdemDeServico.Recebida, inicio);
        AdicionarHistorico(context, ordemId, StatusOrdemDeServico.EmDiagnostico, inicio.AddHours(2));
        AdicionarHistorico(context, ordemId, StatusOrdemDeServico.AguardandoAprovacao, inicio.AddHours(3));
        await context.SaveChangesAsync();

        var resultado = await repositorio.ObterTempoMedioPorStatusAsync();

        resultado.Should().ContainKey(StatusOrdemDeServico.Recebida)
            .WhoseValue.Should().BeApproximately(2.0, 0.001);
        resultado.Should().ContainKey(StatusOrdemDeServico.EmDiagnostico)
            .WhoseValue.Should().BeApproximately(1.0, 0.001);
        resultado.Should().NotContainKey(StatusOrdemDeServico.AguardandoAprovacao);
    }

    // HistoricoStatus define DataAlteracao = DateTime.UtcNow no construtor (sem setter publico) -
    // precisa de reflection so pra controlar o timestamp com precisao neste teste.
    private static void AdicionarHistorico(AppDbContext context, Guid ordemId, StatusOrdemDeServico status, DateTime dataAlteracao)
    {
        var historico = new HistoricoStatus(ordemId, status, "teste");
        typeof(HistoricoStatus)
            .GetProperty(nameof(HistoricoStatus.DataAlteracao))!
            .SetValue(historico, dataAlteracao);

        context.HistoricoStatus.Add(historico);
    }
}
