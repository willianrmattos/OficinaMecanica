using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Commands.RecusarOrcamento;

public record RecusarOrcamentoCommand(Guid OrdemDeServicoId, string? Motivo = null) : IRequest<OrdemDeServicoDto>;
