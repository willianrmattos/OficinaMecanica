using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ObterServico;

public class ObterServicoPorIdQueryHandler : IRequestHandler<ObterServicoPorIdQuery, ServicoDto?>
{
    private readonly IServicoRepository _servicoRepository;

    public ObterServicoPorIdQueryHandler(IServicoRepository servicoRepository)
    {
        _servicoRepository = servicoRepository;
    }

    public async Task<ServicoDto?> Handle(ObterServicoPorIdQuery request, CancellationToken cancellationToken)
    {
        var servico = await _servicoRepository.ObterPorIdAsync(request.Id, cancellationToken);
        if (servico is null) return null;

        return new ServicoDto(servico.Id, servico.Nome, servico.Descricao, servico.Preco, servico.TempoEstimadoMinutos, servico.Ativo);
    }
}
