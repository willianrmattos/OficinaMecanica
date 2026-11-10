using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class OrcamentoNaoGeradoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private string _motivo = "Valor total acima do limite do orçamento.";

    public OrcamentoNaoGeradoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public OrcamentoNaoGeradoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public OrcamentoNaoGeradoBuilder ComMotivo(string motivo) { _motivo = motivo; return this; }

    public OrcamentoNaoGerado Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _motivo);
}
