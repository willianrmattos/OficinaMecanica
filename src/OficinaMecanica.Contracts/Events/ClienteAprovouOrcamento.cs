namespace OficinaMecanica.Contracts.Events;

public sealed record ClienteAprovouOrcamento(
    Guid MessageId,
    Guid OrdemDeServicoId,
    DateTimeOffset OcorridoEm);
