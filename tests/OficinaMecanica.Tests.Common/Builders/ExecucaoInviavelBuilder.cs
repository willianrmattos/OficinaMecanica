using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class ExecucaoInviavelBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private string _motivo = "Peça do fabricante descontinuada.";

    public ExecucaoInviavelBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public ExecucaoInviavelBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public ExecucaoInviavelBuilder ComMotivo(string motivo) { _motivo = motivo; return this; }

    public ExecucaoInviavel Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _motivo);
}
