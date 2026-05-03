using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ObterPeca;

public class ObterPecaPorIdQueryHandler : IRequestHandler<ObterPecaPorIdQuery, PecaDto?>
{
    private readonly IPecaRepository _pecaRepository;

    public ObterPecaPorIdQueryHandler(IPecaRepository pecaRepository)
    {
        _pecaRepository = pecaRepository;
    }

    public async Task<PecaDto?> Handle(ObterPecaPorIdQuery request, CancellationToken cancellationToken)
    {
        var peca = await _pecaRepository.ObterPorIdAsync(request.Id, cancellationToken);
        if (peca is null) return null;

        return new PecaDto(peca.Id, peca.Nome, peca.Descricao, peca.CodigoReferencia,
            peca.PrecoUnitario, peca.QuantidadeEstoque, peca.EstoqueMinimo, peca.Ativo, peca.EstoqueAbaixoDoMinimo);
    }
}
