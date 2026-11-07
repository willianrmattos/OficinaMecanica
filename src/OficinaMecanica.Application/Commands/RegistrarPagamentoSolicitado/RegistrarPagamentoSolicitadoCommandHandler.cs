using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.RegistrarPagamentoSolicitado;

public class RegistrarPagamentoSolicitadoCommandHandler : IRequestHandler<RegistrarPagamentoSolicitadoCommand, Unit>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegistrarPagamentoSolicitadoCommandHandler(IOrdemDeServicoRepository ordemRepository, IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    // A idempotencia (mesmo pagamento registrado de novo) mora em OrdemDeServico.RegistrarPagamentoSolicitado:
    // ela precisa enxergar o PagamentoId ja gravado, que e estado do agregado.
    public async Task<Unit> Handle(RegistrarPagamentoSolicitadoCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        ordem.RegistrarPagamentoSolicitado(request.PagamentoId, request.LinkPagamento, request.ExpiraEm);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
