using MassTransit;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// So o cancelamento do staff desfaz a producao. A Execucao recusa (DomainException, vira Fault) o cancelamento de reparo
// em andamento, e a saga nao muda de estado por isso: reparo em andamento so se desfaz por ExecucaoInviavel.
public sealed class CancelarProducaoNaExecucaoActivity : AtividadeDaSaga
{
    public CancelarProducaoNaExecucaoActivity(TimeProvider relogio) : base(relogio)
    {
    }

    protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto)
    {
        var saga = contexto.Saga;

        return contexto.EnviarAsync(Filas.Execucao, new CancelarProducao(
            IdentificadorDeMensagem.Para(saga.CorrelationId, nameof(CancelarProducao)),
            saga.CorrelationId,
            Agora,
            saga.MotivoCancelamento ?? NormalizadorDeMotivo.MotivoPadrao));
    }
}
