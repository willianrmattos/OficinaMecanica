using OficinaMecanica.Contracts.Common;

namespace OficinaMecanica.Contracts.Events;

public sealed record PrazoExpirado(
    Guid MessageId,
    Guid OrdemDeServicoId,
    DateTimeOffset OcorridoEm,
    Guid PrazoId,
    TipoPrazo Tipo);
