using MediatR;

namespace OficinaMecanica.Application.Commands.IniciarExecucaoDaOrdem;

public record IniciarExecucaoDaOrdemCommand(Guid OrdemDeServicoId) : IRequest<Unit>;
