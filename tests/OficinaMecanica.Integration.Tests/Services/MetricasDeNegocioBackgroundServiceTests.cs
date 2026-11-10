using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OficinaMecanica.API.BackgroundServices;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Integration.Tests.Services;

// O ciclo de coleta das metricas de negocio: o tempo medio por status (que ja existia) e, no mesmo ciclo, o retrato das sagas que
// alimenta os gauges. Roda um ciclo de verdade (o primeiro acontece na partida do servico) sobre EF InMemory.
public class MetricasDeNegocioBackgroundServiceTests
{
    private static async Task<MetricasDeNegocioBackgroundService> CriarAsync(
        Mock<IOrdemDeServicoRepository> repositorio, Action<AppDbContext>? semear = null)
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddSingleton(TimeProvider.System);
        servicos.AddSingleton(Options.Create(new SagaOptions()));
        servicos.AddSingleton(new Mock<IMediator>().Object);
        servicos.AddSingleton(repositorio.Object);
        var banco = "metricas_" + Guid.NewGuid();
        servicos.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(banco));
        servicos.AddScoped<ColetorDeMetricasDaSaga>();

        var provedor = servicos.BuildServiceProvider();
        using (var escopo = provedor.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            semear?.Invoke(db);
            await db.SaveChangesAsync();
        }

        return new MetricasDeNegocioBackgroundService(
            provedor.GetRequiredService<IServiceScopeFactory>(),
            provedor.GetRequiredService<ILogger<MetricasDeNegocioBackgroundService>>());
    }

    private static async Task<List<(string Instrumento, double Valor, string? Estado)>> RodarUmCicloAsync(
        MetricasDeNegocioBackgroundService servico, Mock<IOrdemDeServicoRepository> repositorio)
    {
        var medicoes = new ConcurrentQueue<(string, double, string?)>();
        using var ouvinte = new MeterListener();
        ouvinte.InstrumentPublished = (instrumento, l) =>
        {
            if (instrumento.Meter.Name == MetricasDaSaga.MeterName)
                l.EnableMeasurementEvents(instrumento);
        };
        ouvinte.SetMeasurementEventCallback<long>((i, valor, tags, _) =>
            medicoes.Enqueue((i.Name, valor, tags.ToArray().FirstOrDefault(t => t.Key == "estado").Value?.ToString())));
        ouvinte.Start();

        await servico.StartAsync(CancellationToken.None);

        // O primeiro ciclo roda na partida; espera a coleta da saga (que vem depois do tempo medio) com teto de 10 s.
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            ouvinte.RecordObservableInstruments();
            if (medicoes.Any(m => m.Item1 == "saga_compensacao_presa"))
                break;

            if (DateTime.UtcNow > limite)
                throw new TimeoutException("O ciclo de metricas nao coletou o retrato das sagas em 10 s.");

            await Task.Delay(25);
        }

        await servico.StopAsync(CancellationToken.None);
        repositorio.Verify(r => r.ObterTempoMedioPorStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
        return medicoes.ToList();
    }

    [Fact]
    public async Task Ciclo_DeveColetarOTempoMedioPorStatusEORetratoDasSagasNoMesmoCiclo()
    {
        MetricasDaSaga.Limpar();
        var repositorio = new Mock<IOrdemDeServicoRepository>();
        repositorio.Setup(r => r.ObterTempoMedioPorStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<StatusOrdemDeServico, double> { [StatusOrdemDeServico.Recebida] = 2.5 });
        var servico = await CriarAsync(repositorio, db => db.Set<OrdemDeServicoSagaState>().Add(new OrdemDeServicoSagaState
        {
            CorrelationId = Guid.NewGuid(),
            CurrentState = "EmExecucao",
            Numero = "OS-1",
            CriadaEm = DateTimeOffset.UtcNow,
            AtualizadaEm = DateTimeOffset.UtcNow
        }));

        var medicoes = await RodarUmCicloAsync(servico, repositorio);

        medicoes.Should().Contain(m => m.Instrumento == "saga_por_estado" && m.Estado == "EmExecucao" && m.Valor == 1);
        medicoes.Where(m => m.Instrumento == "saga_compensacao_presa").Select(m => m.Estado)
            .Should().BeEquivalentTo("CancelandoPagamento", "Estornando", "EstornoPendenteManual");
        MetricasDaSaga.Limpar();
    }

    [Fact]
    public async Task Ciclo_ComFalhaNoTempoMedio_DeveAindaColetarOsGaugesDaSaga()
    {
        MetricasDaSaga.Limpar();
        var repositorio = new Mock<IOrdemDeServicoRepository>();
        repositorio.Setup(r => r.ObterTempoMedioPorStatusAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("banco fora"));
        var servico = await CriarAsync(repositorio);

        var medicoes = await RodarUmCicloAsync(servico, repositorio);

        medicoes.Should().Contain(m => m.Instrumento == "saga_compensacao_presa", "uma coleta que falha nao pode impedir a outra");
        MetricasDaSaga.Limpar();
    }
}
