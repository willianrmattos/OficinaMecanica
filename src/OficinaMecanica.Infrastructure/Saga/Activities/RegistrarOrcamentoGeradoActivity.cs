using MassTransit;
using MediatR;
using OficinaMecanica.Application.Commands.RegistrarOrcamentoGerado;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// OS vai para AguardandoAprovacao e o prazo de aprovacao nasce junto, na mesma transacao: nao existe orcamento esperando
// decisao sem um timer correndo para ele. O prazo e o ValidoAte que o proprio Billing confirmou.
public sealed class RegistrarOrcamentoGeradoActivity : AtividadeDaSaga<OrcamentoGerado>
{
    private readonly IMediator _mediator;
    private readonly IPrazoRepository _prazos;

    public RegistrarOrcamentoGeradoActivity(IMediator mediator, IPrazoRepository prazos, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
        _prazos = prazos;
    }

    protected override async Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState, OrcamentoGerado> contexto)
    {
        var saga = contexto.Saga;

        await _mediator.Send(new RegistrarOrcamentoGeradoCommand(saga.CorrelationId), contexto.CancellationToken);

        var prazo = new SagaPrazo(saga.CorrelationId, Guid.NewGuid(), TipoPrazo.Aprovacao, contexto.Message.ValidoAte);
        await _prazos.AdicionarAsync(prazo, contexto.CancellationToken);

        saga.OrcamentoId = contexto.Message.OrcamentoId;
        saga.PrazoAprovacaoId = prazo.PrazoId;
    }
}
