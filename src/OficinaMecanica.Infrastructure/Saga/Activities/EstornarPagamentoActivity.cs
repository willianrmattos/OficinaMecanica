using MassTransit;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Devolve o dinheiro de um pagamento ja aprovado. O id do envelope inclui o PagamentoId: o estorno e por pagamento, e o
// reenvio do mesmo comando nunca pode estornar duas vezes.
public sealed class EstornarPagamentoActivity : AtividadeDaSaga
{
    public EstornarPagamentoActivity(TimeProvider relogio) : base(relogio)
    {
    }

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto)
    {
        var saga = contexto.Saga;
        var pagamentoId = saga.PagamentoId
            ?? throw new InvalidOperationException($"A saga da OS {saga.CorrelationId} precisa estornar um pagamento, mas não conhece o PagamentoId.");

        return contexto.EnviarAsync(Filas.Billing, new EstornarPagamento(
            IdentificadorDeMensagem.Para(saga.CorrelationId, nameof(EstornarPagamento), pagamentoId),
            saga.CorrelationId,
            Agora,
            pagamentoId,
            saga.MotivoCancelamento ?? NormalizadorDeMotivo.MotivoPadrao));
    }
}
