using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class EstornoFalhouBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private Guid _pagamentoId = Guid.NewGuid();
    private string _erro = "O gateway de pagamento está indisponível.";
    private DateTimeOffset _ocorridoEm = DateTimeOffset.UtcNow;

    public EstornoFalhouBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public EstornoFalhouBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public EstornoFalhouBuilder ComPagamento(Guid pagamentoId) { _pagamentoId = pagamentoId; return this; }
    public EstornoFalhouBuilder ComErro(string erro) { _erro = erro; return this; }
    public EstornoFalhouBuilder ComOcorridoEm(DateTimeOffset ocorridoEm) { _ocorridoEm = ocorridoEm; return this; }

    public EstornoFalhou Build() => new(_messageId, _ordemDeServicoId, _ocorridoEm, _pagamentoId, _erro);
}
