using OficinaMecanica.Domain.Common;

namespace OficinaMecanica.Domain.Events;

public class OrcamentoAprovadoEvent : DomainEvent
{
    public Guid OrdemDeServicoId { get; }
    public string Numero { get; }

    public OrcamentoAprovadoEvent(Guid ordemDeServicoId, string numero)
    {
        OrdemDeServicoId = ordemDeServicoId;
        Numero = numero;
    }
}
