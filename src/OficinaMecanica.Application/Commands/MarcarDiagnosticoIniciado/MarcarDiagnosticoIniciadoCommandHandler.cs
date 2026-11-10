using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.MarcarDiagnosticoIniciado;

public class MarcarDiagnosticoIniciadoCommandHandler : IRequestHandler<MarcarDiagnosticoIniciadoCommand, Unit>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MarcarDiagnosticoIniciadoCommandHandler(IOrdemDeServicoRepository ordemRepository, IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(MarcarDiagnosticoIniciadoCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        if (ordem.JaAlcancou(StatusOrdemDeServico.EmDiagnostico))
            return Unit.Value;

        ordem.AvancarParaDiagnostico();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
