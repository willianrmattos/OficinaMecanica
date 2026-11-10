using OficinaMecanica.Domain.Common;

namespace OficinaMecanica.Domain.Events;

public class PagamentoSolicitadoEvent : DomainEvent
{
    public Guid OrdemDeServicoId { get; }
    public string Numero { get; }
    public Guid ClienteId { get; }
    public string LinkPagamento { get; }
    public DateTimeOffset ExpiraEm { get; }

    public PagamentoSolicitadoEvent(Guid ordemDeServicoId, string numero, Guid clienteId, string linkPagamento, DateTimeOffset expiraEm)
    {
        OrdemDeServicoId = ordemDeServicoId;
        Numero = numero;
        ClienteId = clienteId;
        LinkPagamento = linkPagamento;
        ExpiraEm = expiraEm;
    }
}
