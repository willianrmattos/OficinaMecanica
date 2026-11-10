namespace OficinaMecanica.Contracts.Events;

public sealed record CancelamentoSolicitado(
    Guid MessageId,
    Guid OrdemDeServicoId,
    DateTimeOffset OcorridoEm,
    string Motivo);
