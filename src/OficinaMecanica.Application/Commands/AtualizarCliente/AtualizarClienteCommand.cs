using MediatR;

namespace OficinaMecanica.Application.Commands.AtualizarCliente;

public record AtualizarClienteCommand(
    Guid Id,
    string Nome,
    string? Email,
    string? Telefone
) : IRequest;
