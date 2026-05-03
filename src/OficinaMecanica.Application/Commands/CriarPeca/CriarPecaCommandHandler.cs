using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.CriarPeca;

public class CriarPecaCommandHandler : IRequestHandler<CriarPecaCommand, PecaDto>
{
    private readonly IPecaRepository _pecaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CriarPecaCommandHandler(IPecaRepository pecaRepository, IUnitOfWork unitOfWork)
    {
        _pecaRepository = pecaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PecaDto> Handle(CriarPecaCommand request, CancellationToken cancellationToken)
    {
        var peca = new Peca(request.Nome, request.Descricao, request.CodigoReferencia, request.PrecoUnitario, request.QuantidadeEstoque, request.EstoqueMinimo);

        await _pecaRepository.AdicionarAsync(peca, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PecaDto(peca.Id, peca.Nome, peca.Descricao, peca.CodigoReferencia, peca.PrecoUnitario, peca.QuantidadeEstoque, peca.EstoqueMinimo, peca.Ativo, peca.EstoqueAbaixoDoMinimo);
    }
}
