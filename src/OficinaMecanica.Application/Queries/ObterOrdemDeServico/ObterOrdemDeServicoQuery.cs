using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ObterOrdemDeServico;

public record ObterOrdemDeServicoPorIdQuery(Guid Id) : IRequest<OrdemDeServicoDto?>;
public record ObterOrdemDeServicoPorNumeroQuery(string Numero) : IRequest<OrdemDeServicoDto?>;
