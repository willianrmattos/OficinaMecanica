using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ObterServico;

public record ObterServicoPorIdQuery(Guid Id) : IRequest<ServicoDto?>;
