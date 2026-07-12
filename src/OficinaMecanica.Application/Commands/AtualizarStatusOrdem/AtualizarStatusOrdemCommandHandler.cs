using MediatR;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
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

    public async Task<Unit> Handle(AtualizarStatusOrdemCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterPorIdAsync(request.OrdemDeServicoId, cancellationToken)
            ?? throw new DomainException("Ordem de serviço não encontrada.");

        if (!Enum.IsDefined(typeof(StatusOrdemDeServico), request.NovoStatus))
            throw new DomainException($"Status '{request.NovoStatus}' é inválido.");

        var novoStatus = (StatusOrdemDeServico)request.NovoStatus;

        switch (novoStatus)
        {
            case StatusOrdemDeServico.EmDiagnostico:
                ordem.AvancarParaDiagnostico();
                break;
            case StatusOrdemDeServico.AguardandoAprovacao:
                ordem.EnviarParaAprovacao();
                break;
            case StatusOrdemDeServico.Finalizada:
                ordem.Finalizar();
                break;
            case StatusOrdemDeServico.Entregue:
                ordem.Entregar();
                break;
            case StatusOrdemDeServico.EmExecucao:
                throw new DomainException("Para colocar a ordem em execução é necessário aprová-la pelo endpoint de aprovação.");
            case StatusOrdemDeServico.OrcamentoRecusado:
                throw new DomainException("Para recusar o orçamento é necessário usar o endpoint de recusa.");
            default:
                throw new DomainException($"Transição para o status '{novoStatus}' não é suportada.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
