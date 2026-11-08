using MassTransit;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Abre a saga e pede o diagnostico a Execucao. A mensagem leva o retrato da OS no momento da abertura: a Execucao
// nunca consulta o banco daqui.
public sealed class IniciarSagaActivity : AtividadeDaSaga<OrdemDeServicoAberta>
{
    public IniciarSagaActivity(TimeProvider relogio) : base(relogio)
    {
    }

    protected override async Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState, OrdemDeServicoAberta> contexto)
    {
        var aberta = contexto.Message;
        var saga = contexto.Saga;

        saga.Numero = aberta.Numero;
        saga.CriadaEm = Agora;

        await contexto.EnviarAsync(Filas.Execucao, new SolicitarDiagnostico(
            IdentificadorDeMensagem.Para(saga.CorrelationId, nameof(SolicitarDiagnostico)),
            saga.CorrelationId,
            Agora,
            aberta.Numero,
            aberta.Placa,
            aberta.Marca,
            aberta.Modelo,
            aberta.Observacoes,
            aberta.Servicos,
            aberta.Pecas));
    }
}
