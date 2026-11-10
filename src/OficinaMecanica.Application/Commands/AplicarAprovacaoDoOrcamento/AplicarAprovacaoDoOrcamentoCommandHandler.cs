using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento;

public class AplicarAprovacaoDoOrcamentoCommandHandler : IRequestHandler<AplicarAprovacaoDoOrcamentoCommand, Unit>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AplicarAprovacaoDoOrcamentoCommandHandler(IOrdemDeServicoRepository ordemRepository, IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(AplicarAprovacaoDoOrcamentoCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        if (ordem.JaAlcancou(StatusOrdemDeServico.AguardandoPagamento))
            return Unit.Value;

        ordem.AprovarOrcamento();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
