using MediatR;

namespace OficinaMecanica.Application.Commands.FinalizarOrdem;

public record FinalizarOrdemCommand(Guid OrdemDeServicoId) : IRequest<Unit>;
