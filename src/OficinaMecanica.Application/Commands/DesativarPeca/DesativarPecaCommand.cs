using MediatR;

namespace OficinaMecanica.Application.Commands.DesativarPeca;

public record DesativarPecaCommand(Guid Id, bool Ativar = false) : IRequest;
