using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ObterPeca;

public record ObterPecaPorIdQuery(Guid Id) : IRequest<PecaDto?>;
