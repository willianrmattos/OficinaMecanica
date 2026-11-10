using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class DiagnosticoIniciadoBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private string _mecanicoId = $"mecanico-{Guid.NewGuid():N}";

    public DiagnosticoIniciadoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public DiagnosticoIniciadoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public DiagnosticoIniciadoBuilder DoMecanico(string mecanicoId) { _mecanicoId = mecanicoId; return this; }

    public DiagnosticoIniciado Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _mecanicoId);
}
