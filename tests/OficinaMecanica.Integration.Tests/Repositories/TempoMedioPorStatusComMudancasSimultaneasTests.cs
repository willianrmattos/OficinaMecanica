using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Integration.Tests.Repositories;

// Classe propria, com banco proprio: a media por status agrega o historico de todas as OS do banco, entao o teste precisa de uma OS so.
public class TempoMedioPorStatusComMudancasSimultaneasTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TempoMedioPorStatusComMudancasSimultaneasTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ObterTempoMedioPorStatusAsync_MudancasNoMesmoInstanteGravadasForaDeOrdem_DeveAtribuirADuracaoAoStatusQueAntecedeOProximo()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repositorio = scope.ServiceProvider.GetRequiredService<IOrdemDeServicoRepository>();

        var ordemId = Guid.NewGuid();
        var inicio = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);

        // Recebida e EmDiagnostico no mesmo instante, lidas na ordem inversa: Recebida dura 0h e EmDiagnostico, 3h ate a aprovacao.
        context.HistoricoStatus.AddRange(
            HistoricoEmbaralhado.Linha(ordemId, StatusOrdemDeServico.EmDiagnostico, inicio),
            HistoricoEmbaralhado.Linha(ordemId, StatusOrdemDeServico.Recebida, inicio),
            HistoricoEmbaralhado.Linha(ordemId, StatusOrdemDeServico.AguardandoAprovacao, inicio.AddHours(3)));
        await context.SaveChangesAsync();

        var resultado = await repositorio.ObterTempoMedioPorStatusAsync();

        resultado.Should().ContainKey(StatusOrdemDeServico.Recebida).WhoseValue.Should().BeApproximately(0.0, 0.001);
        resultado.Should().ContainKey(StatusOrdemDeServico.EmDiagnostico).WhoseValue.Should().BeApproximately(3.0, 0.001);
        resultado.Should().NotContainKey(StatusOrdemDeServico.AguardandoAprovacao);
    }
}
