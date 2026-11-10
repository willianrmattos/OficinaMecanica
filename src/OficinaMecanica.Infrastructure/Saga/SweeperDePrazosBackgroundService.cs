using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OficinaMecanica.Infrastructure.Saga;

// Chama o sweeper a cada SweeperIntervaloSegundos. Cada varredura roda em escopo proprio (AppDbContext e outbox novos) e
// uma falha so e logada: o proximo ciclo tenta de novo, e um banco fora do ar nao pode derrubar o processo. Com varias
// replicas (HPA) todas rodam este laco; o claim do repositorio garante que cada prazo e tratado por uma so.
public sealed class SweeperDePrazosBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;
    private readonly SagaOptions _opcoes;
    private readonly ILogger<SweeperDePrazosBackgroundService> _logger;

    public SweeperDePrazosBackgroundService(
        IServiceScopeFactory escopos,
        TimeProvider relogio,
        IOptions<SagaOptions> opcoes,
        ILogger<SweeperDePrazosBackgroundService> logger)
    {
        _escopos = escopos;
        _relogio = relogio;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opcoes.SweeperHabilitado)
        {
            _logger.LogInformation("Sweeper de prazos da saga desabilitado por configuração (Saga:SweeperHabilitado).");
            return;
        }

        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(_opcoes.SweeperIntervaloSegundos), _relogio);

        try
        {
            do
            {
                await VarrerAsync(stoppingToken);
            }
            while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Parada normal do host.
        }
    }

    private async Task VarrerAsync(CancellationToken cancellationToken)
    {
        try
        {
            int reivindicados;
            do
            {
                await using var escopo = _escopos.CreateAsyncScope();
                var sweeper = escopo.ServiceProvider.GetRequiredService<SweeperDePrazos>();
                reivindicados = await sweeper.ProcessarLoteAsync(cancellationToken);
            }
            // Lote cheio indica fila de prazos vencidos maior que o lote: segue sem esperar o proximo ciclo.
            while (reivindicados >= _opcoes.SweeperLote && !cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na varredura de prazos da saga; tento de novo no próximo ciclo.");
        }
    }
}
