using MassTransit;
using MediatR;
using Microsoft.Extensions.Options;
using OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Common;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Estoque ja reservado: a OS vai para AguardandoPagamento, nasce o prazo de pagamento e o Billing e chamado a gerar o link.
// O prazo de aprovacao deixa de ser o vigente (o cliente ja decidiu), entao um aviso atrasado dele nao decide mais nada.
public sealed class AplicarAprovacaoDoOrcamentoActivity : AtividadeDaSaga
{
    private readonly IMediator _mediator;
    private readonly IPrazoRepository _prazos;
    private readonly SagaOptions _opcoes;

    public AplicarAprovacaoDoOrcamentoActivity(IMediator mediator, IPrazoRepository prazos, IOptions<SagaOptions> opcoes, TimeProvider relogio)
        : base(relogio)
    {
        _mediator = mediator;
        _prazos = prazos;
        _opcoes = opcoes.Value;
    }

    protected override async Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto)
    {
        var saga = contexto.Saga;
        var orcamentoId = saga.OrcamentoId
            ?? throw new InvalidOperationException($"A saga da OS {saga.CorrelationId} aprovou um orçamento que não conhece (OrcamentoId nulo).");

        await _mediator.Send(new AplicarAprovacaoDoOrcamentoCommand(saga.CorrelationId), contexto.CancellationToken);

        var expiraEm = Agora.AddHours(_opcoes.PrazoPagamentoHoras);
        var prazo = new SagaPrazo(saga.CorrelationId, Guid.NewGuid(), TipoPrazo.Pagamento, expiraEm);
        await _prazos.AdicionarAsync(prazo, contexto.CancellationToken);

        saga.PrazoAprovacaoId = null;
        saga.PrazoPagamentoId = prazo.PrazoId;

        // O id inclui o prazo de pagamento: uma segunda janela de pagamento (se um dia existir) e um comando novo.
        await contexto.EnviarAsync(Filas.Billing, new SolicitarPagamento(
            IdentificadorDeMensagem.Para(saga.CorrelationId, nameof(SolicitarPagamento), prazo.PrazoId),
            saga.CorrelationId,
            Agora,
            orcamentoId,
            expiraEm));
    }
}
