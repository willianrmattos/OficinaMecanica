using MediatR;

namespace OficinaMecanica.Application.Commands.AtualizarPeca;

public record AtualizarPecaCommand(
    Guid Id,
    string Nome,
    string? Descricao,
    string? CodigoReferencia,
    decimal PrecoUnitario,
    int EstoqueMinimo
) : IRequest;
