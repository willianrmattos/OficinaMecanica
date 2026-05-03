using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Commands.CriarPeca;

public record CriarPecaCommand(
    string Nome,
    string? Descricao,
    string? CodigoReferencia,
    decimal PrecoUnitario,
    int QuantidadeEstoque,
    int EstoqueMinimo
) : IRequest<PecaDto>;
