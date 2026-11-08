using System.Runtime.CompilerServices;
using MassTransit;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Infrastructure.Saga;

// Emite as metricas de transicao da saga sem tocar na maquina alem de se conectar a ela. O MassTransit chama PostExecute so
// depois de TODOS os passos do evento terem rodado sem excecao, entao uma transicao que falha num passo (e que o retry do
// endpoint refaz) nao conta. Mas a emissao acontece ANTES do commit da transacao do consumer (EF e outbox): se o commit falhar e
// o retry reprocessar a mensagem, a transicao e contada de novo. A contagem pode ficar a MAIOR que o numero de transicoes
// confirmadas, nunca a menor; quem precisa do numero exato le os gauges, que partem do banco. Optei por nao mover a emissao
// para depois do commit: exigiria um ponto pos-commit no pipeline do consumer so para uma janela rara e de erro do proprio banco.
//
// Uma transicao e "estado antes != estado depois". O estado e o AtualizadaEm de ANTES so existem no PreExecute: depois, os passos
// ja carimbaram AtualizadaEm. Guardo os dois numa tabela fraca presa a instancia da saga (a mesma nos dois momentos), que se
// esvazia sozinha e nao precisa de limpeza.
internal sealed class ObservadorDeMetricasDaSaga : IEventObserver<OrdemDeServicoSagaState>
{
    private readonly ConditionalWeakTable<OrdemDeServicoSagaState, EstadoAntes> _antes = new();

    public Task PreExecute(BehaviorContext<OrdemDeServicoSagaState> context)
    {
        Guardar(context.Saga);
        return Task.CompletedTask;
    }

    public Task PreExecute<T>(BehaviorContext<OrdemDeServicoSagaState, T> context) where T : class
    {
        Guardar(context.Saga);
        return Task.CompletedTask;
    }

    public Task PostExecute(BehaviorContext<OrdemDeServicoSagaState> context)
    {
        Registrar(context.Saga, evento: null, mensagem: null);
        return Task.CompletedTask;
    }

    public Task PostExecute<T>(BehaviorContext<OrdemDeServicoSagaState, T> context) where T : class
    {
        Registrar(context.Saga, NomeDoEvento(typeof(T)), context.Message);
        return Task.CompletedTask;
    }

    public Task ExecuteFault(BehaviorContext<OrdemDeServicoSagaState> context, Exception exception)
    {
        _antes.Remove(context.Saga);
        return Task.CompletedTask;
    }

    public Task ExecuteFault<T>(BehaviorContext<OrdemDeServicoSagaState, T> context, Exception exception) where T : class
    {
        _antes.Remove(context.Saga);
        return Task.CompletedTask;
    }

    private void Guardar(OrdemDeServicoSagaState saga) =>
        _antes.AddOrUpdate(saga, new EstadoAntes(saga.CurrentState, saga.AtualizadaEm));

    private void Registrar(OrdemDeServicoSagaState saga, string? evento, object? mensagem)
    {
        if (!_antes.TryGetValue(saga, out var antes))
            return;

        _antes.Remove(saga);

        if (string.Equals(antes.Estado, saga.CurrentState, StringComparison.Ordinal))
            return;

        var origem = EhInicial(antes.Estado) ? null : antes.Estado;

        switch (saga.CurrentState)
        {
            case "Concluida":
                MetricasDaSaga.Concluida.Add(1);
                break;
            case "Cancelada":
                MetricasDaSaga.Cancelada.Add(1, new KeyValuePair<string, object?>("estado_origem", origem ?? "Initial"));
                break;
            case "OrcamentoRecusado":
                MetricasDaSaga.OrcamentoRecusado.Add(1, new KeyValuePair<string, object?>("causa", evento ?? "desconhecida"));
                break;
            case "EstornoPendenteManual":
                MetricasDaSaga.EstornoPendenteManual.Add(1, new KeyValuePair<string, object?>("causa", evento ?? "desconhecida"));
                break;
        }

        if (origem is null)
        {
            MetricasDaSaga.Iniciada.Add(1);
            return;
        }

        if (mensagem is PrazoExpirado prazo)
            MetricasDaSaga.PrazoExpirado.Add(1, new KeyValuePair<string, object?>("tipo", prazo.Tipo.ToString()));

        var segundos = (saga.AtualizadaEm - antes.AtualizadaEm).TotalSeconds;
        MetricasDaSaga.DuracaoDoEstado.Record(Math.Max(0, segundos), new KeyValuePair<string, object?>("estado", origem));
    }

    // Instancia nova ainda sem estado (a abertura da saga): o MassTransit a trata como Initial.
    private static bool EhInicial(string estado) => string.IsNullOrEmpty(estado) || estado == "Initial";

    // Fault<EstornarPagamento> em vez de Fault`1: o nome vai numa tag que o alerta le.
    private static string NomeDoEvento(Type tipo)
    {
        if (!tipo.IsGenericType)
            return tipo.Name;

        var nome = tipo.Name[..tipo.Name.IndexOf('`')];
        return $"{nome}<{string.Join(", ", tipo.GetGenericArguments().Select(a => a.Name))}>";
    }

    private sealed record EstadoAntes(string Estado, DateTimeOffset AtualizadaEm);
}
