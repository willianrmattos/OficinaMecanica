using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class PagamentoSolicitadoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private Guid _pagamentoId = Guid.NewGuid();
    private string _link = "https://pagamento.exemplo.com/checkout/abc123";
    private DateTimeOffset _expiraEm = DateTimeOffset.UtcNow.AddDays(2);
    private DateTimeOffset _ocorridoEm = DateTimeOffset.UtcNow;

    public PagamentoSolicitadoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public PagamentoSolicitadoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public PagamentoSolicitadoBuilder ComPagamento(Guid pagamentoId) { _pagamentoId = pagamentoId; return this; }
    public PagamentoSolicitadoBuilder ComLink(string link) { _link = link; return this; }
    public PagamentoSolicitadoBuilder ExpiraEm(DateTimeOffset expiraEm) { _expiraEm = expiraEm; return this; }
    public PagamentoSolicitadoBuilder ComOcorridoEm(DateTimeOffset ocorridoEm) { _ocorridoEm = ocorridoEm; return this; }

    public PagamentoSolicitado Build() => new(_messageId, _ordemDeServicoId, _ocorridoEm, _pagamentoId, _link, _expiraEm);
}
