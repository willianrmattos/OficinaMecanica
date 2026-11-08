using OficinaMecanica.Application.Observabilidade;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.API.BackgroundServices;

// Recalcula periodicamente porque o tempo medio por status depende do historico
// inteiro (nao de um evento pontual) - nao da pra emitir isso reagindo a um unico
// DomainEvent como as outras metricas de negocio. O mesmo vale para os gauges da saga (quantas ha em cada estado, quantas
// presas em compensacao): sao retratos do banco, entao entram nesta mesma coleta em vez de ganhar uma tarefa de fundo propria.
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

            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ColetorDeMetricasDaSaga>().ColetarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao coletar as métricas de estado da saga.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
