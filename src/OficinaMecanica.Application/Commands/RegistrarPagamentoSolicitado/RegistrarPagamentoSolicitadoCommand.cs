using MediatR;

namespace OficinaMecanica.Application.Commands.RegistrarPagamentoSolicitado;

public record RegistrarPagamentoSolicitadoCommand(
    Guid OrdemDeServicoId,
    Guid PagamentoId,
    string LinkPagamento,
    DateTimeOffset ExpiraEm
) : IRequest<Unit>;
