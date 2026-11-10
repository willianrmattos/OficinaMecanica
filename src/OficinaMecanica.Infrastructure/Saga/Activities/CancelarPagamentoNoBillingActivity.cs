using MassTransit;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// O Billing e a autoridade sobre o pagamento: este comando pede o cancelamento e ele responde PagamentoCancelado, ou
// PagamentoAprovado se o dinheiro ja entrou. PagamentoId pode ser nulo (PagamentoSolicitado ainda nao chegou): o
// Billing localiza pela OS. O id do envelope inclui o prazo de pagamento, a janela que esta sendo cancelada.
public sealed class CancelarPagamentoNoBillingActivity : AtividadeDaSaga
{
    public CancelarPagamentoNoBillingActivity(TimeProvider relogio) : base(relogio)
    {
    }

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto)
    {
        var saga = contexto.Saga;

        return contexto.EnviarAsync(Filas.Billing, new CancelarPagamento(
            IdentificadorDeMensagem.Para(saga.CorrelationId, nameof(CancelarPagamento), saga.PrazoPagamentoId),
            saga.CorrelationId,
            Agora,
            saga.PagamentoId,
            saga.MotivoCancelamento ?? NormalizadorDeMotivo.MotivoPadrao));
    }
}
