using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ListarServicos;

public record ListarServicosQuery(
    int Pagina = 1,
    int TamanhoPagina = 10,
    bool ApenasAtivos = true
) : IRequest<PaginacaoResultDto<ServicoDto>>;
