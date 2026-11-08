using MassTransit;
using Microsoft.Extensions.Logging;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Decisao de uma pessoa (aprovar, recusar, cancelar) que a API ja aceitou com 202 e que a saga nao pode mais aplicar: o
// prazo decidiu antes, o reparo ja comecou ou a OS ja fechou. Ela continua sem efeito, mas deixa este aviso com o id da OS e
// o da mensagem: sem ele ninguem acharia por que uma aprovacao aceita nao aconteceu. Nao carimba AtualizadaEm: o aviso nao
// e atualizacao da saga.
public sealed class LogarDecisaoIgnoradaActivity<TMensagem> : AtividadeDaSaga<TMensagem>
    where TMensagem : class
{
    private readonly ILogger<LogarDecisaoIgnoradaActivity<TMensagem>> _logger;

    public LogarDecisaoIgnoradaActivity(ILogger<LogarDecisaoIgnoradaActivity<TMensagem>> logger, TimeProvider relogio) : base(relogio)
    {
        _logger = logger;
    }

    protected override bool CarimbaAtualizacao => false;

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState, TMensagem> contexto)
    {
        _logger.LogWarning(
            "Decisão {Decisao} da ordem de serviço {OrdemDeServicoId} ignorada: a saga está em {Estado} e a decisão não tem mais efeito, mensagem {MessageIdDaDecisao}",
            typeof(TMensagem).Name,
            contexto.Saga.CorrelationId,
            contexto.Saga.CurrentState,
            contexto.MessageId);

        MetricasDaSaga.DecisaoIgnorada.Add(
            1,
            new KeyValuePair<string, object?>("decisao", typeof(TMensagem).Name),
            new KeyValuePair<string, object?>("estado", contexto.Saga.CurrentState));

        return Task.CompletedTask;
    }
}
