using MediatR;

namespace OficinaMecanica.Application.Commands.RemoverCliente;

public record RemoverClienteCommand(Guid Id) : IRequest;
