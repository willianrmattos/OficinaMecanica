using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class PrazoExpiradoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private TipoPrazo _tipo = TipoPrazo.Aprovacao;
    private Guid _prazoId = Guid.NewGuid();

    public PrazoExpiradoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public PrazoExpiradoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public PrazoExpiradoBuilder DoTipo(TipoPrazo tipo) { _tipo = tipo; return this; }
    public PrazoExpiradoBuilder ComPrazo(Guid prazoId) { _prazoId = prazoId; return this; }

    public PrazoExpirado Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _prazoId, _tipo);
}
