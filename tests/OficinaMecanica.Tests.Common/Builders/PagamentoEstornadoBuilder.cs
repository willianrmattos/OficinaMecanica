using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class PagamentoEstornadoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private Guid _pagamentoId = Guid.NewGuid();
    private string _gatewayRefundId = $"refund-{Guid.NewGuid():N}";

    public PagamentoEstornadoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public PagamentoEstornadoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public PagamentoEstornadoBuilder ComPagamento(Guid pagamentoId) { _pagamentoId = pagamentoId; return this; }

    public PagamentoEstornado Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _pagamentoId, _gatewayRefundId);
}
