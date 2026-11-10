using MassTransit;
using MediatR;
using OficinaMecanica.Application.Commands.FinalizarOrdem;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

public sealed class FinalizarOrdemActivity : AtividadeDaSaga
{
    private readonly IMediator _mediator;

    public FinalizarOrdemActivity(IMediator mediator, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
    }

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto) =>
        _mediator.Send(new FinalizarOrdemCommand(contexto.Saga.CorrelationId), contexto.CancellationToken);
}
