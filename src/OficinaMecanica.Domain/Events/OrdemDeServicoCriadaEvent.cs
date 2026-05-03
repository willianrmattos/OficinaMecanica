using OficinaMecanica.Domain.Common;

namespace OficinaMecanica.Domain.Events;

public class OrdemDeServicoCriadaEvent : DomainEvent
{
    public Guid OrdemDeServicoId { get; }
    public string Numero { get; }
    public Guid ClienteId { get; }

    public OrdemDeServicoCriadaEvent(Guid ordemDeServicoId, string numero, Guid clienteId)
    {
        OrdemDeServicoId = ordemDeServicoId;
        Numero = numero;
        ClienteId = clienteId;
    }
}
