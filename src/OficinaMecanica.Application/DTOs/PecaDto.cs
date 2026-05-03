namespace OficinaMecanica.Application.DTOs;

public record PecaDto(
    Guid Id,
    string Nome,
    string? Descricao,
    string? CodigoReferencia,
    decimal PrecoUnitario,
    int QuantidadeEstoque,
    int EstoqueMinimo,
    bool Ativo,
    bool EstoqueAbaixoDoMinimo
);
