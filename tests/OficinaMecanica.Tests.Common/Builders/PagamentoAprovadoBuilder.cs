using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class PagamentoAprovadoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private Guid _pagamentoId = Guid.NewGuid();
    private string _gatewayPaymentId = $"mp-{Guid.NewGuid():N}";
    private decimal _valor = 100m;

    public PagamentoAprovadoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public PagamentoAprovadoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public PagamentoAprovadoBuilder ComPagamento(Guid pagamentoId) { _pagamentoId = pagamentoId; return this; }
    public PagamentoAprovadoBuilder ComValor(decimal valor) { _valor = valor; return this; }

    public PagamentoAprovado Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _pagamentoId, _gatewayPaymentId, _valor);
}
