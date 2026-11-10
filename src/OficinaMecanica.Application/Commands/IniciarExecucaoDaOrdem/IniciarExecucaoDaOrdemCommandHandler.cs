using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.IniciarExecucaoDaOrdem;

public class IniciarExecucaoDaOrdemCommandHandler : IRequestHandler<IniciarExecucaoDaOrdemCommand, Unit>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public IniciarExecucaoDaOrdemCommandHandler(IOrdemDeServicoRepository ordemRepository, IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(IniciarExecucaoDaOrdemCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        if (ordem.JaAlcancou(StatusOrdemDeServico.EmExecucao))
            return Unit.Value;

        ordem.IniciarExecucao();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
