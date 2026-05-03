using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AdicionarEstoquePeca;

public class AdicionarEstoquePecaCommandHandler : IRequestHandler<AdicionarEstoquePecaCommand, PecaDto>
{
    private readonly IPecaRepository _pecaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdicionarEstoquePecaCommandHandler(IPecaRepository pecaRepository, IUnitOfWork unitOfWork)
    {
        _pecaRepository = pecaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PecaDto> Handle(AdicionarEstoquePecaCommand request, CancellationToken cancellationToken)
    {
        var peca = await _pecaRepository.ObterPorIdAsync(request.Id, cancellationToken)
            ?? throw new DomainException("Peça não encontrada.");

        peca.AdicionarEstoque(request.Quantidade);

        _pecaRepository.Atualizar(peca);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PecaDto(peca.Id, peca.Nome, peca.Descricao, peca.CodigoReferencia,
            peca.PrecoUnitario, peca.QuantidadeEstoque, peca.EstoqueMinimo, peca.Ativo, peca.EstoqueAbaixoDoMinimo);
    }
}
