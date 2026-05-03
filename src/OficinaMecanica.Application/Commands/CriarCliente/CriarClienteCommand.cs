using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Commands.CriarCliente;

public record CriarClienteCommand(
    string Nome,
    string Documento,
    string? Email,
    string? Telefone
) : IRequest<ClienteDto>;
