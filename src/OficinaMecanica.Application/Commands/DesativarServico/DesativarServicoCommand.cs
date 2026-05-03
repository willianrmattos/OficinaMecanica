using MediatR;

namespace OficinaMecanica.Application.Commands.DesativarServico;

public record DesativarServicoCommand(Guid Id, bool Ativar = false) : IRequest;
