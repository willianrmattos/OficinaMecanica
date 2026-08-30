using MediatR;
using OficinaMecanica.Application.Observabilidade;
using OficinaMecanica.Domain.Events;

namespace OficinaMecanica.Application.EventHandlers;

public class OrdemDeServicoCriadaMetricaEventHandler : INotificationHandler<OrdemDeServicoCriadaEvent>
{
    public Task Handle(OrdemDeServicoCriadaEvent notification, CancellationToken cancellationToken)
    {
        MetricasNegocio.OrdensCriadas.Add(1);
        return Task.CompletedTask;
    }
}

public class OrcamentoRecusadoMetricaEventHandler : INotificationHandler<OrcamentoRecusadoEvent>
{
    public Task Handle(OrcamentoRecusadoEvent notification, CancellationToken cancellationToken)
    {
        MetricasNegocio.OrcamentosRecusados.Add(1, new KeyValuePair<string, object?>("motivo", notification.Motivo ?? "nao_informado"));
        return Task.CompletedTask;
    }
}
