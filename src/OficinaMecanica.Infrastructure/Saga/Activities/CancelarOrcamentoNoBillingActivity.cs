using MassTransit;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// O Billing nao cancela o orcamento em cascata quando o pagamento falha ou o estoque falta: so este comando o cancela.
// OrcamentoId pode ser nulo (cancelamento antes de o orcamento existir): o Billing localiza pela OS e, se ainda nao ha
// orcamento, registra o cancelamento previo.
//
// O motivo e o que a saga guardou ao entrar no caminho de compensacao; so quando ela nao guardou nenhum (a origem nao
// mandou texto) entra o motivo padrao, que a subclasse especializa para o extrato do Billing dizer a causa real.
public class CancelarOrcamentoNoBillingActivity : AtividadeDaSaga
{
    public CancelarOrcamentoNoBillingActivity(TimeProvider relogio) : base(relogio)
    {
    }

    protected virtual string MotivoPadrao => NormalizadorDeMotivo.MotivoPadrao;

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto)
    {
        var saga = contexto.Saga;

        return contexto.EnviarAsync(Filas.Billing, new CancelarOrcamento(
            IdentificadorDeMensagem.Para(saga.CorrelationId, nameof(CancelarOrcamento)),
            saga.CorrelationId,
            Agora,
            saga.OrcamentoId,
            saga.MotivoCancelamento ?? MotivoPadrao));
    }
}
