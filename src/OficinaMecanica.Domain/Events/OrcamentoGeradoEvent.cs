using OficinaMecanica.Domain.Common;

namespace OficinaMecanica.Domain.Events;

public class OrcamentoGeradoEvent : DomainEvent
{
    public Guid OrdemDeServicoId { get; }
    public string Numero { get; }
    public decimal ValorTotal { get; }

    public OrcamentoGeradoEvent(Guid ordemDeServicoId, string numero, decimal valorTotal)
    {
        OrdemDeServicoId = ordemDeServicoId;
        Numero = numero;
        ValorTotal = valorTotal;
    }
}
