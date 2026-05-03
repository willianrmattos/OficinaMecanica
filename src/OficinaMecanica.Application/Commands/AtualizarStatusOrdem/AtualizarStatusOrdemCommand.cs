using MediatR;

namespace OficinaMecanica.Application.Commands.AtualizarStatusOrdem;

public record AtualizarStatusOrdemCommand(
    Guid OrdemDeServicoId,
    int NovoStatus
) : IRequest<Unit>;
