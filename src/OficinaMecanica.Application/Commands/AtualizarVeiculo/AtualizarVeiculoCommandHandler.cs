using MediatR;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AtualizarVeiculo;

public class AtualizarVeiculoCommandHandler : IRequestHandler<AtualizarVeiculoCommand>
{
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AtualizarVeiculoCommandHandler(IVeiculoRepository veiculoRepository, IUnitOfWork unitOfWork)
    {
        _veiculoRepository = veiculoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AtualizarVeiculoCommand request, CancellationToken cancellationToken)
    {
        var veiculo = await _veiculoRepository.ObterPorIdAsync(request.Id, cancellationToken)
            ?? throw new DomainException("Veículo não encontrado.");

        veiculo.Atualizar(request.Marca, request.Modelo, request.Ano);

        _veiculoRepository.Atualizar(veiculo);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
