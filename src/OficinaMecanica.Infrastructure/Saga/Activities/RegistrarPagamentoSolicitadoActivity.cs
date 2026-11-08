using MassTransit;
using MediatR;
using OficinaMecanica.Application.Commands.RegistrarPagamentoSolicitado;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Grava o link e o prazo na OS (o e-mail ao cliente sai do domain event, depois do commit) e guarda o PagamentoId, que a
// saga precisa para cancelar ou estornar. A saga continua em AguardandoPagamento: o prazo de pagamento ja nasceu quando
// o SolicitarPagamento foi enviado. Repetir o mesmo evento nao muda nada (o comando e idempotente).
public sealed class RegistrarPagamentoSolicitadoActivity : AtividadeDaSaga<PagamentoSolicitado>
{
    private readonly IMediator _mediator;

    public RegistrarPagamentoSolicitadoActivity(IMediator mediator, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
    }

    protected override async Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState, PagamentoSolicitado> contexto)
    {
        var solicitado = contexto.Message;

        await _mediator.Send(
            new RegistrarPagamentoSolicitadoCommand(contexto.Saga.CorrelationId, solicitado.PagamentoId, solicitado.LinkPagamento, solicitado.ExpiraEm),
            contexto.CancellationToken);

        contexto.Saga.PagamentoId = solicitado.PagamentoId;
    }
}
