namespace OficinaMecanica.Application.DTOs;

public record ItemServicoDto(
    Guid Id,
    Guid ServicoId,
    string NomeServico,
    decimal PrecoUnitario,
    int Quantidade,
    decimal Subtotal
);
