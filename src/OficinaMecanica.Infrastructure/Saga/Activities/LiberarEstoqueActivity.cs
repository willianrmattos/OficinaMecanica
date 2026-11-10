using MassTransit;
using MediatR;
using OficinaMecanica.Application.Commands.LiberarEstoque;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Devolve as pecas reservadas. O comando e idempotente (sem reserva nao devolve nada), entao a activity pode rodar em todo
// desfecho que cancela depois de uma aprovacao sem perguntar se a reserva existiu.
public sealed class LiberarEstoqueActivity : AtividadeDaSaga
{
    private readonly IMediator _mediator;

    public LiberarEstoqueActivity(IMediator mediator, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
    }

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto) =>
        _mediator.Send(new LiberarEstoqueCommand(contexto.Saga.CorrelationId), contexto.CancellationToken);
}
