using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ListarPecas;

public record ListarPecasQuery(
    int Pagina = 1,
    int TamanhoPagina = 10,
    bool ApenasAtivos = true
) : IRequest<PaginacaoResultDto<PecaDto>>;
