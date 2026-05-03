namespace OficinaMecanica.Application.DTOs;

public record ServicoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    decimal Preco,
    int TempoEstimadoMinutos,
    bool Ativo
);
