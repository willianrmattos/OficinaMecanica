using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class ClienteAprovouOrcamentoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();

    public ClienteAprovouOrcamentoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public ClienteAprovouOrcamentoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }

    public ClienteAprovouOrcamento Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow);
}
