using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ObterTempoMedioServicos;

public record ObterTempoMedioServicosQuery() : IRequest<TempoMedioServicoDto>;
