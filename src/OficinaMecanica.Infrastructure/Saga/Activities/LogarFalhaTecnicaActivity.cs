using MassTransit;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Contracts.Commands;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Fault<T> e falha TECNICA do destino (excecao que esgotou o retry dele), nao uma resposta de negocio: o Billing e a
// Execucao respondem com evento explicito quando o problema e de regra. Entao o primeiro passo e deixar o sinal duravel
// no log, com os ids que permitem achar a mensagem na fila _error do destino; a compensacao (quando ha) vem depois,
// nos passos que a maquina de estados encadeia.
public sealed class LogarFalhaTecnicaActivity<TComando> : AtividadeDaSaga<Fault<TComando>>
    where TComando : class
{
    private readonly ILogger<LogarFalhaTecnicaActivity<TComando>> _logger;

    public LogarFalhaTecnicaActivity(ILogger<LogarFalhaTecnicaActivity<TComando>> logger, TimeProvider relogio) : base(relogio)
    {
        _logger = logger;
    }

    private static readonly bool EhCompensacao =
        typeof(TComando) == typeof(CancelarPagamento) || typeof(TComando) == typeof(CancelarOrcamento) || typeof(TComando) == typeof(CancelarProducao);

    protected override bool CarimbaAtualizacao => false;

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState, Fault<TComando>> contexto)
    {
        var falha = contexto.Message;
        var causa = falha.Exceptions?.FirstOrDefault();

        _logger.LogError(
            "Falha técnica no comando {Comando} da ordem de serviço {OrdemDeServicoId} (saga em {Estado}), mensagem {MessageIdDoComando} no host {Host}: {TipoDaExcecao}: {MensagemDaExcecao}",
            typeof(TComando).Name,
            contexto.Saga.CorrelationId,
            contexto.Saga.CurrentState,
            falha.FaultedMessageId,
            falha.Host?.MachineName,
            causa?.ExceptionType,
            causa?.Message);

        // Os tres comandos de compensacao nao tem reenvio nem transicao: o log acima e esta metrica sao o sinal de que o
        // comando espera o redrive manual na fila de erro do destino.
        if (EhCompensacao)
            MetricasDaSaga.CompensacaoFalhou.Add(1, new KeyValuePair<string, object?>("comando", typeof(TComando).Name));

        return Task.CompletedTask;
    }
}
