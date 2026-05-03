using MediatR;

namespace OficinaMecanica.Application.Commands.AtualizarServico;

public record AtualizarServicoCommand(
    Guid Id,
    string Nome,
    string? Descricao,
    decimal Preco,
    int TempoEstimadoMinutos
) : IRequest;
