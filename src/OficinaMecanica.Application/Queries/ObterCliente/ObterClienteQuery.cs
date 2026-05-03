using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ObterCliente;

public record ObterClientePorIdQuery(Guid Id) : IRequest<ClienteDto?>;
