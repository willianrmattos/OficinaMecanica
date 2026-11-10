using MassTransit;
using MediatR;
using OficinaMecanica.Application.Commands.AplicarRecusaDoOrcamento;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// OS vai para OrcamentoRecusado, por recusa do cliente ou por prazo vencido. O prazo de aprovacao deixa de ser o vigente.
public sealed class AplicarRecusaDoOrcamentoActivity : AtividadeDaSaga
{
    private readonly IMediator _mediator;

    public AplicarRecusaDoOrcamentoActivity(IMediator mediator, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
    }

    protected override async Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto)
    {
        var saga = contexto.Saga;

        await _mediator.Send(new AplicarRecusaDoOrcamentoCommand(saga.CorrelationId, saga.MotivoCancelamento), contexto.CancellationToken);

        saga.PrazoAprovacaoId = null;
    }
}
