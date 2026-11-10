using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AtualizarStatusOrdem;

public class AtualizarStatusOrdemCommandHandler : IRequestHandler<AtualizarStatusOrdemCommand, Unit>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AtualizarStatusOrdemCommandHandler(
        IOrdemDeServicoRepository ordemRepository,
        IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    // O validator so deixa passar 'Entregue': a entrega e a unica etapa manual, as demais vem da saga.
    public async Task<Unit> Handle(AtualizarStatusOrdemCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        ordem.Entregar();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
