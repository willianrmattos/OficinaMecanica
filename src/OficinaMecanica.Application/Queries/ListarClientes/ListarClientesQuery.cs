using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ListarClientes;

public record ListarClientesQuery(
    int Pagina = 1,
    int TamanhoPagina = 10,
    string? FiltroNome = null
) : IRequest<PaginacaoResultDto<ClienteDto>>;
