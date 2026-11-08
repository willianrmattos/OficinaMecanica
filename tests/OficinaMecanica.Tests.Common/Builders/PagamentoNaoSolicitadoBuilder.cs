using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class PagamentoNaoSolicitadoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private string _motivo = "O gateway recusou a criação da cobrança.";

    public PagamentoNaoSolicitadoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public PagamentoNaoSolicitadoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public PagamentoNaoSolicitadoBuilder ComMotivo(string motivo) { _motivo = motivo; return this; }

    public PagamentoNaoSolicitado Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _motivo);
}
