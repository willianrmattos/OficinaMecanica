using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class PagamentoCanceladoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private Guid? _pagamentoId = Guid.NewGuid();

    public PagamentoCanceladoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public PagamentoCanceladoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public PagamentoCanceladoBuilder ComPagamento(Guid pagamentoId) { _pagamentoId = pagamentoId; return this; }

    // Cancelamento que chegou ao Billing antes de existir pagamento: a resposta sai sem PagamentoId.
    public PagamentoCanceladoBuilder SemPagamento() { _pagamentoId = null; return this; }

    public PagamentoCancelado Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _pagamentoId);
}
