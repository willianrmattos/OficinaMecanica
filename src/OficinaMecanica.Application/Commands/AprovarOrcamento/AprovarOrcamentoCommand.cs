using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Commands.AprovarOrcamento;

public record AprovarOrcamentoCommand(Guid OrdemDeServicoId) : IRequest<OrdemDeServicoDto>;
