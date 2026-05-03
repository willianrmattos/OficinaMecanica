using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ListarServicos;

public class ListarServicosQueryHandler : IRequestHandler<ListarServicosQuery, PaginacaoResultDto<ServicoDto>>
{
    private readonly IServicoRepository _servicoRepository;

    public ListarServicosQueryHandler(IServicoRepository servicoRepository)
    {
        _servicoRepository = servicoRepository;
    }

    public async Task<PaginacaoResultDto<ServicoDto>> Handle(ListarServicosQuery request, CancellationToken cancellationToken)
    {
        var servicos = await _servicoRepository.ListarAsync(request.Pagina, request.TamanhoPagina, request.ApenasAtivos, cancellationToken);
        var total = await _servicoRepository.ContarAsync(request.ApenasAtivos, cancellationToken);

        var dtos = servicos.Select(s => new ServicoDto(s.Id, s.Nome, s.Descricao, s.Preco, s.TempoEstimadoMinutos, s.Ativo));

        return new PaginacaoResultDto<ServicoDto>(
            dtos, total, request.Pagina, request.TamanhoPagina,
            (int)Math.Ceiling(total / (double)request.TamanhoPagina)
        );
    }
}
