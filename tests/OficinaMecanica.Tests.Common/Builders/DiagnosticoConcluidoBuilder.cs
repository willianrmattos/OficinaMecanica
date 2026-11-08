using Bogus;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class DiagnosticoConcluidoBuilder
{
    private static readonly Faker Faker = new("pt_BR");

    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private string _mecanicoId = $"mecanico-{Guid.NewGuid():N}";
    private string _laudo = Faker.Lorem.Sentence();

    public DiagnosticoConcluidoBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public DiagnosticoConcluidoBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public DiagnosticoConcluidoBuilder DoMecanico(string mecanicoId) { _mecanicoId = mecanicoId; return this; }
    public DiagnosticoConcluidoBuilder ComLaudo(string laudo) { _laudo = laudo; return this; }

    public DiagnosticoConcluido Build() => new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _mecanicoId, _laudo);
}
