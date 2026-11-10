using MassTransit;
using MediatR;
using OficinaMecanica.Application.Commands.IniciarExecucaoDaOrdem;
using OficinaMecanica.Application.Queries.ObterSnapshotDaOrdem;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Pagamento aprovado: a OS vai para EmExecucao e a Execucao recebe o snapshot do que foi aprovado e pago (servicos e
// pecas de agora, nao os do diagnostico). O prazo de pagamento deixa de ser o vigente.
public sealed class IniciarExecucaoActivity : AtividadeDaSaga
{
    private readonly IMediator _mediator;

    public IniciarExecucaoActivity(IMediator mediator, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
    }

    protected override async Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto)
    {
        var saga = contexto.Saga;

        await _mediator.Send(new IniciarExecucaoDaOrdemCommand(saga.CorrelationId), contexto.CancellationToken);
        var snapshot = await _mediator.Send(new ObterSnapshotDaOrdemQuery(saga.CorrelationId), contexto.CancellationToken);

        saga.PrazoPagamentoId = null;

        await contexto.EnviarAsync(Filas.Execucao, new IniciarExecucao(
            IdentificadorDeMensagem.Para(saga.CorrelationId, nameof(IniciarExecucao)),
            saga.CorrelationId,
            Agora,
            snapshot.Numero,
            snapshot.Servicos,
            snapshot.Pecas));
    }
}
