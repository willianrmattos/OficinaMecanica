using MassTransit;
using MediatR;
using OficinaMecanica.Application.Commands.CancelarOrdemPelaSaga;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Cancela a OS com o motivo que a saga guardou ao entrar no caminho de compensacao. Nao libera estoque: a liberacao e uma
// activity propria, porque nem todo cancelamento tem reserva a devolver.
public sealed class CancelarOrdemActivity : AtividadeDaSaga
{
    private readonly IMediator _mediator;

    public CancelarOrdemActivity(IMediator mediator, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
    }

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto) =>
        _mediator.Send(new CancelarOrdemPelaSagaCommand(contexto.Saga.CorrelationId, contexto.Saga.MotivoCancelamento), contexto.CancellationToken);
}
