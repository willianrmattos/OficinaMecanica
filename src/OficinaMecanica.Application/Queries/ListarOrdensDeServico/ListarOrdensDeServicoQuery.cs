using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ListarOrdensDeServico;

public record ListarOrdensDeServicoQuery(
    int Pagina = 1,
    int TamanhoPagina = 10,
    string? FiltroStatus = null
) : IRequest<PaginacaoResultDto<OrdemDeServicoDto>>;
