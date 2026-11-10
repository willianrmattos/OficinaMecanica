using MediatR;

namespace OficinaMecanica.Application.Commands.LiberarEstoque;

public record LiberarEstoqueCommand(Guid OrdemDeServicoId) : IRequest<Unit>;
