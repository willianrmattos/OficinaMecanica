using MassTransit;
using MediatR;
using OficinaMecanica.Application.Commands.MarcarDiagnosticoIniciado;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

public sealed class RegistrarDiagnosticoIniciadoActivity : AtividadeDaSaga
{
    private readonly IMediator _mediator;

    public RegistrarDiagnosticoIniciadoActivity(IMediator mediator, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
    }

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto) =>
        _mediator.Send(new MarcarDiagnosticoIniciadoCommand(contexto.Saga.CorrelationId), contexto.CancellationToken);
}
