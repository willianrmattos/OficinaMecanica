using MassTransit;
using MediatR;
using Microsoft.Extensions.Options;
using OficinaMecanica.Application.Queries.ObterSnapshotDaOrdem;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Diagnostico concluido: pede o orcamento ao Billing com o valor e os itens que a OS tem agora (podem ter mudado durante
// o diagnostico) e o laudo do mecanico. ValidoAte e o prazo de aprovacao, que o Billing grava no orcamento e valida de novo.
public sealed class SolicitarOrcamentoActivity : AtividadeDaSaga<DiagnosticoConcluido>
{
    private readonly IMediator _mediator;
    private readonly SagaOptions _opcoes;

    public SolicitarOrcamentoActivity(IMediator mediator, IOptions<SagaOptions> opcoes, TimeProvider relogio) : base(relogio)
    {
        _mediator = mediator;
        _opcoes = opcoes.Value;
    }

    protected override async Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState, DiagnosticoConcluido> contexto)
    {
        var saga = contexto.Saga;
        var snapshot = await _mediator.Send(new ObterSnapshotDaOrdemQuery(saga.CorrelationId), contexto.CancellationToken);

        await contexto.EnviarAsync(Filas.Billing, new GerarOrcamento(
            IdentificadorDeMensagem.Para(saga.CorrelationId, nameof(GerarOrcamento)),
            saga.CorrelationId,
            Agora,
            snapshot.Numero,
            snapshot.ValorTotal,
            Agora.AddHours(_opcoes.PrazoAprovacaoHoras),
            contexto.Message.Laudo,
            snapshot.Servicos,
            snapshot.Pecas));
    }
}
