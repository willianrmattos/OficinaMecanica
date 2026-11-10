using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AplicarRecusaDoOrcamento;

public class AplicarRecusaDoOrcamentoCommandHandler : IRequestHandler<AplicarRecusaDoOrcamentoCommand, Unit>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AplicarRecusaDoOrcamentoCommandHandler(IOrdemDeServicoRepository ordemRepository, IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(AplicarRecusaDoOrcamentoCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        if (ordem.Status == StatusOrdemDeServico.OrcamentoRecusado)
            return Unit.Value;

        ordem.RecusarOrcamento(request.Motivo);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
