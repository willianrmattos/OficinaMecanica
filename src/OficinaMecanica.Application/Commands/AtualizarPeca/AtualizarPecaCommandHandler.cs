using MediatR;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AtualizarPeca;

public class AtualizarPecaCommandHandler : IRequestHandler<AtualizarPecaCommand>
{
    private readonly IPecaRepository _pecaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AtualizarPecaCommandHandler(IPecaRepository pecaRepository, IUnitOfWork unitOfWork)
    {
        _pecaRepository = pecaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AtualizarPecaCommand request, CancellationToken cancellationToken)
    {
        var peca = await _pecaRepository.ObterPorIdAsync(request.Id, cancellationToken)
            ?? throw new DomainException("Peça não encontrada.");

        peca.Atualizar(request.Nome, request.Descricao, request.CodigoReferencia, request.PrecoUnitario, request.EstoqueMinimo);

        _pecaRepository.Atualizar(peca);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
