using MediatR;

namespace OficinaMecanica.Application.Commands.RegistrarOrcamentoGerado;

public record RegistrarOrcamentoGeradoCommand(Guid OrdemDeServicoId) : IRequest<Unit>;
