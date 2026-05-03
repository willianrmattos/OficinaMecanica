using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ObterVeiculo;

public record ObterVeiculoPorIdQuery(Guid Id) : IRequest<VeiculoDto?>;
