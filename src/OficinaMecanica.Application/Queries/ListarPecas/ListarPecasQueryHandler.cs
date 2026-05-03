using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ListarPecas;

public class ListarPecasQueryHandler : IRequestHandler<ListarPecasQuery, PaginacaoResultDto<PecaDto>>
{
    private readonly IPecaRepository _pecaRepository;

    public ListarPecasQueryHandler(IPecaRepository pecaRepository)
    {
        _pecaRepository = pecaRepository;
    }

    public async Task<PaginacaoResultDto<PecaDto>> Handle(ListarPecasQuery request, CancellationToken cancellationToken)
    {
        var pecas = await _pecaRepository.ListarAsync(request.Pagina, request.TamanhoPagina, request.ApenasAtivos, cancellationToken);
        var total = await _pecaRepository.ContarAsync(request.ApenasAtivos, cancellationToken);

        var dtos = pecas.Select(p => new PecaDto(p.Id, p.Nome, p.Descricao, p.CodigoReferencia, p.PrecoUnitario, p.QuantidadeEstoque, p.EstoqueMinimo, p.Ativo, p.EstoqueAbaixoDoMinimo));

        return new PaginacaoResultDto<PecaDto>(
            dtos, total, request.Pagina, request.TamanhoPagina,
            (int)Math.Ceiling(total / (double)request.TamanhoPagina)
        );
    }
}
