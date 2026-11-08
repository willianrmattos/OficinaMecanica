using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class ExecucaoFinalizadaBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private string _resumo = "Serviço executado conforme o orçamento aprovado.";

    public ExecucaoFinalizadaBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public ExecucaoFinalizadaBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public ExecucaoFinalizadaBuilder ComResumo(string resumo) { _resumo = resumo; return this; }

    public ExecucaoFinalizada Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _resumo, DateTimeOffset.UtcNow);
}
