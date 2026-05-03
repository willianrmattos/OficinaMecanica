namespace OficinaMecanica.Application.DTOs;

public record HistoricoStatusDto(
    Guid Id,
    string Status,
    string Observacao,
    DateTime DataAlteracao
);
