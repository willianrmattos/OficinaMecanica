using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class CancelamentoSolicitadoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private string _motivo = "Cliente desistiu do serviço.";

    public CancelamentoSolicitadoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public CancelamentoSolicitadoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public CancelamentoSolicitadoBuilder ComMotivo(string motivo) { _motivo = motivo; return this; }

    public CancelamentoSolicitado Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _motivo);
}
