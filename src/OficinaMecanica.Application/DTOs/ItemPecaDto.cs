namespace OficinaMecanica.Application.DTOs;

public record ItemPecaDto(
    Guid Id,
    Guid PecaId,
    string NomePeca,
    decimal PrecoUnitario,
    int Quantidade,
    decimal Subtotal
);
