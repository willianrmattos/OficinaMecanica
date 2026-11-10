using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.FinalizarOrdem;

public class FinalizarOrdemCommandHandler : IRequestHandler<FinalizarOrdemCommand, Unit>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public FinalizarOrdemCommandHandler(IOrdemDeServicoRepository ordemRepository, IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(FinalizarOrdemCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        if (ordem.JaAlcancou(StatusOrdemDeServico.Finalizada))
            return Unit.Value;

        ordem.Finalizar();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
