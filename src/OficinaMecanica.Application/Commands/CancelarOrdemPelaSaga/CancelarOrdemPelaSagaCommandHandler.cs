using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.CancelarOrdemPelaSaga;

public class CancelarOrdemPelaSagaCommandHandler : IRequestHandler<CancelarOrdemPelaSagaCommand, Unit>
{
    public const string MotivoPadrao = NormalizadorDeMotivo.MotivoPadrao;

    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelarOrdemPelaSagaCommandHandler(IOrdemDeServicoRepository ordemRepository, IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    // Nao libera estoque: a liberacao e um passo proprio da saga (LiberarEstoqueCommand), porque nem todo
    // cancelamento tem reserva a devolver e porque ela precisa poder ser repetida sozinha ate dar certo.
    public async Task<Unit> Handle(CancelarOrdemPelaSagaCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        if (ordem.Status == StatusOrdemDeServico.Cancelada)
            return Unit.Value;

        ordem.Cancelar(NormalizadorDeMotivo.Normalizar(request.Motivo));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
