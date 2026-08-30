using OficinaMecanica.Application.Observabilidade;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.API.BackgroundServices;

// Recalcula periodicamente porque o tempo medio por status depende do historico
// inteiro (nao de um evento pontual) - nao da pra emitir isso reagindo a um unico
// DomainEvent como as outras metricas de negocio.
public class MetricasDeNegocioBackgroundService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetricasDeNegocioBackgroundService> _logger;

    public MetricasDeNegocioBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MetricasDeNegocioBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repositorio = scope.ServiceProvider.GetRequiredService<IOrdemDeServicoRepository>();
                var tempoPorStatus = await repositorio.ObterTempoMedioPorStatusAsync(stoppingToken);

                foreach (var (status, horas) in tempoPorStatus)
                    MetricasNegocio.TempoExecucaoHoras.Record(horas, new KeyValuePair<string, object?>("status", status.ToString()));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao recalcular métricas de tempo médio por status.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
