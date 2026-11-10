using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class ClienteRecusouOrcamentoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private string? _motivo = "Achei o valor alto.";

    public ClienteRecusouOrcamentoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public ClienteRecusouOrcamentoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public ClienteRecusouOrcamentoBuilder ComMotivo(string? motivo) { _motivo = motivo; return this; }

    public ClienteRecusouOrcamento Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _motivo);
}
