using MediatR;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.RemoverVeiculo;

public class RemoverVeiculoCommandHandler : IRequestHandler<RemoverVeiculoCommand>
{
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoverVeiculoCommandHandler(IVeiculoRepository veiculoRepository, IUnitOfWork unitOfWork)
    {
        _veiculoRepository = veiculoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RemoverVeiculoCommand request, CancellationToken cancellationToken)
    {
        var veiculo = await _veiculoRepository.ObterPorIdAsync(request.Id, cancellationToken)
            ?? throw new DomainException("Veículo não encontrado.");

        _veiculoRepository.Remover(veiculo);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
