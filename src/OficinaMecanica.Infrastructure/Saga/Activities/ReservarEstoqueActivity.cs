using MassTransit;
using MediatR;
using OficinaMecanica.Application.Commands.ReservarEstoque;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Reserva o estoque ANTES de pedir o pagamento: faltando peca, o cliente nunca e cobrado. O resultado vai para o
// contexto do evento (e nao para a saga) porque so serve de condicao ao proximo passo da mesma mensagem e nao deve virar
// coluna do banco.
public sealed class ReservarEstoqueActivity : AtividadeDaSaga
{
    private readonly IMediator _mediator;

    public ReservarEstoqueActivity(IMediator mediator, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
    }

    protected override async Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto)
    {
        var resultado = await _mediator.Send(new ReservarEstoqueCommand(contexto.Saga.CorrelationId), contexto.CancellationToken);

        contexto.GetOrAddPayload(() => new ReservaDeEstoqueDoEvento()).Resultado = resultado;
    }
}
