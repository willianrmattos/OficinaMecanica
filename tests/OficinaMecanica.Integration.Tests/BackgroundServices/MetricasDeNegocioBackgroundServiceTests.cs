using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OficinaMecanica.API.BackgroundServices;
using OficinaMecanica.Application.Observabilidade;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Integration.Tests.BackgroundServices;

// Uso o DI container de verdade (via CustomWebApplicationFactory) em vez de mockar
// IServiceScopeFactory/IServiceScope/IServiceProvider na mao - assim o "encadeamento"
// de scope -> IOrdemDeServicoRepository -> AppDbContext InMemory e testado de verdade.
// MetricasDeNegocioBackgroundService so e registrado como IHostedService no Program.cs
// dentro do "if (!string.IsNullOrWhiteSpace(otelEndpoint))", e Otel:Endpoint fica vazio
// tanto no appsettings.json quanto no .Development.json (ambiente usado por
// CustomWebApplicationFactory) - ou seja, nao existe uma segunda instancia "de
// verdade" rodando nesse host de teste que possa competir com a que eu crio aqui e
// contaminar a medicao.
public class MetricasDeNegocioBackgroundServiceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MetricasDeNegocioBackgroundServiceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExecuteAsync_ComHistoricoDeStatus_DeveRegistrarTempoExecucaoHorasComTagDeStatus()
    {
        var ordemId = Guid.NewGuid();
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        using (var scopeDeSeed = _factory.Services.CreateScope())
        {
            var context = scopeDeSeed.ServiceProvider.GetRequiredService<AppDbContext>();

            // So um par de historico => ObterTempoMedioPorStatusAsync devolve exatamente
            // uma entrada (Recebida, 3h) - EmDiagnostico fica sem par seguinte (status
            // ainda "aberto") e nao entra no calculo, igual ao teste do repositorio.
            AdicionarHistorico(context, ordemId, StatusOrdemDeServico.Recebida, inicio);
            AdicionarHistorico(context, ordemId, StatusOrdemDeServico.EmDiagnostico, inicio.AddHours(3));
            await context.SaveChangesAsync();
        }

        var medicoes = new List<(double Value, KeyValuePair<string, object?>[] Tags)>();
        var medicaoRegistrada = new TaskCompletionSource();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == MetricasNegocio.MeterName && instrument.Name == "tempo_execucao_horas")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            medicoes.Add((measurement, tags.ToArray()));
            medicaoRegistrada.TrySetResult();
        });
        listener.Start();

        var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        var service = new MetricasDeNegocioBackgroundService(scopeFactory, NullLogger<MetricasDeNegocioBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            // O corpo do do-while roda imediatamente ao iniciar (antes de qualquer espera
            // no PeriodicTimer) - so preciso aguardar essa primeira medicao ser publicada,
            // nao um tick real do timer de 5 minutos.
            await medicaoRegistrada.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }

        medicoes.Should().ContainSingle(m =>
            Math.Abs(m.Value - 3.0) < 0.001
            && m.Tags.Any(t => t.Key == "status" && (string?)t.Value == StatusOrdemDeServico.Recebida.ToString()));
    }

    // Reflection so pra controlar DataAlteracao com precisao - HistoricoStatus define
    // esse valor como DateTime.UtcNow no construtor, sem setter publico (mesma tecnica
    // de OrdemDeServicoRepositoryTests).
    private static void AdicionarHistorico(AppDbContext context, Guid ordemId, StatusOrdemDeServico status, DateTime dataAlteracao)
    {
        var historico = new HistoricoStatus(ordemId, status, "teste");
        typeof(HistoricoStatus)
            .GetProperty(nameof(HistoricoStatus.DataAlteracao))!
            .SetValue(historico, dataAlteracao);

        context.HistoricoStatus.Add(historico);
    }
}
