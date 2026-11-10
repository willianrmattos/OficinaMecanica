namespace OficinaMecanica.Contracts.Events;

public sealed record ClienteRecusouOrcamento(
    Guid MessageId,
    Guid OrdemDeServicoId,
    DateTimeOffset OcorridoEm,
    string? Motivo);
