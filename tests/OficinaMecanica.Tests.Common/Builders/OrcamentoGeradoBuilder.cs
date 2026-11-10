using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class OrcamentoGeradoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private Guid _orcamentoId = Guid.NewGuid();
    private decimal _valorTotal = 100m;
    private DateTimeOffset _validoAte = DateTimeOffset.UtcNow.AddDays(3);
    private DateTimeOffset _ocorridoEm = DateTimeOffset.UtcNow;

    public OrcamentoGeradoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public OrcamentoGeradoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public OrcamentoGeradoBuilder ComOrcamento(Guid orcamentoId) { _orcamentoId = orcamentoId; return this; }
    public OrcamentoGeradoBuilder ComValorTotal(decimal valorTotal) { _valorTotal = valorTotal; return this; }
    public OrcamentoGeradoBuilder ValidoAte(DateTimeOffset validoAte) { _validoAte = validoAte; return this; }
    public OrcamentoGeradoBuilder ComOcorridoEm(DateTimeOffset ocorridoEm) { _ocorridoEm = ocorridoEm; return this; }

    public OrcamentoGerado Build() => new(_messageId, _ordemDeServicoId, _ocorridoEm, _orcamentoId, _valorTotal, _validoAte);
}
