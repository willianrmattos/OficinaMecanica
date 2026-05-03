using MediatR;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.DesativarPeca;

public class DesativarPecaCommandHandler : IRequestHandler<DesativarPecaCommand>
{
    private readonly IPecaRepository _pecaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DesativarPecaCommandHandler(IPecaRepository pecaRepository, IUnitOfWork unitOfWork)
    {
        _pecaRepository = pecaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DesativarPecaCommand request, CancellationToken cancellationToken)
    {
        var peca = await _pecaRepository.ObterPorIdAsync(request.Id, cancellationToken)
            ?? throw new DomainException("Peça não encontrada.");

        if (request.Ativar)
            peca.Ativar();
        else
            peca.Desativar();

        _pecaRepository.Atualizar(peca);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
