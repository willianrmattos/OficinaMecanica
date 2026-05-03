using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Enums;

namespace OficinaMecanica.Domain.Events;

public class StatusOrdemAlteradoEvent : DomainEvent
{
    public Guid OrdemDeServicoId { get; }
    public string Numero { get; }
    public StatusOrdemDeServico NovoStatus { get; }

    public StatusOrdemAlteradoEvent(Guid ordemDeServicoId, string numero, StatusOrdemDeServico novoStatus)
    {
        OrdemDeServicoId = ordemDeServicoId;
        Numero = numero;
        NovoStatus = novoStatus;
    }
}
