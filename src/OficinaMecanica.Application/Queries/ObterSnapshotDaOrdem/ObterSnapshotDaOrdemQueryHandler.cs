using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ObterSnapshotDaOrdem;

public class ObterSnapshotDaOrdemQueryHandler : IRequestHandler<ObterSnapshotDaOrdemQuery, SnapshotDaOrdemDto>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;

    public ObterSnapshotDaOrdemQueryHandler(IOrdemDeServicoRepository ordemRepository)
    {
        _ordemRepository = ordemRepository;
    }

    // Ordem inexistente continua DomainException (400), como nos commands internos: a saga so pergunta por OS que ela mesma conduz.
    public async Task<SnapshotDaOrdemDto> Handle(ObterSnapshotDaOrdemQuery request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        return new SnapshotDaOrdemDto(
            ordem.Numero,
            ordem.ValorTotal,
            ordem.ItensServico
                .Select(i => new ItemDeServicoMensagem(i.ServicoId, i.NomeServico, i.PrecoUnitario, i.Quantidade))
                .ToList(),
            ordem.ItensPeca
                .Select(i => new ItemDePecaMensagem(i.PecaId, i.NomePeca, i.PrecoUnitario, i.Quantidade))
                .ToList());
    }
}
