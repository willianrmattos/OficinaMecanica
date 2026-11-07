using MediatR;

namespace OficinaMecanica.Application.Commands.ReservarEstoque;

public record ReservarEstoqueCommand(Guid OrdemDeServicoId) : IRequest<ResultadoDaReservaDeEstoque>;
