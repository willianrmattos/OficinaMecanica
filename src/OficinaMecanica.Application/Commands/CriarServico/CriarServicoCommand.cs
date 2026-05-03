using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Commands.CriarServico;

public record CriarServicoCommand(
    string Nome,
    string? Descricao,
    decimal Preco,
    int TempoEstimadoMinutos
) : IRequest<ServicoDto>;
