using MassTransit;
using Microsoft.Extensions.Logging;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Estorno que nao andou: a saga fica em EstornoPendenteManual (nao vira Cancelada, o dinheiro ainda nao voltou) e este e o
// alerta. Nenhuma decisao automatica e tomada: o staff refaz o estorno na API do Billing e o PagamentoEstornado resultante
// e o que fecha a saga. O log de erro e o sinal que o alarme operacional observa (procure "EstornoPendenteManual").
//
// A causa vem de mensagens diferentes (resposta de negocio do Billing ou Fault tecnico), entao cada uma tem a sua subclasse
// com a extracao da causa: nenhuma recebe um tipo que ela nao saiba ler.
public abstract class AlertarEstornoPendenteActivity<TMensagem> : AtividadeDaSaga<TMensagem>
    where TMensagem : class
{
    private readonly ILogger _logger;

    protected AlertarEstornoPendenteActivity(ILogger logger, TimeProvider relogio) : base(relogio)
    {
        _logger = logger;
    }

    protected abstract string? CausaDe(TMensagem mensagem);

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState, TMensagem> contexto)
    {
        _logger.LogError(
            "Estorno da ordem de serviço {OrdemDeServicoId} (pagamento {PagamentoId}) não concluído; a saga fica em EstornoPendenteManual até o staff refazer o estorno no Billing. Causa: {Causa}",
            contexto.Saga.CorrelationId,
            contexto.Saga.PagamentoId,
            CausaDe(contexto.Message));

        return Task.CompletedTask;
    }
}
