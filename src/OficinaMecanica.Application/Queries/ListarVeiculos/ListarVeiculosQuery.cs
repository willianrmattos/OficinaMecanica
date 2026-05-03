using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ListarVeiculos;

public record ListarVeiculosPorClienteQuery(Guid ClienteId) : IRequest<IEnumerable<VeiculoDto>>;
