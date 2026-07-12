using OficinaMecanica.Domain.Common;

namespace OficinaMecanica.Domain.Events;

public class OrcamentoRecusadoEvent : DomainEvent
{
    public Guid OrdemDeServicoId { get; }
    public string Numero { get; }
    public string? Motivo { get; }

    public OrcamentoRecusadoEvent(Guid ordemDeServicoId, string numero, string? motivo)
    {
        OrdemDeServicoId = ordemDeServicoId;
        Numero = numero;
        Motivo = motivo;
    }
}
