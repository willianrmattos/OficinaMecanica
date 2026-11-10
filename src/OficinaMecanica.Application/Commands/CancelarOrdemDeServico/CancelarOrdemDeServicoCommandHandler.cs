using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.CancelarOrdemDeServico;

public class CancelarOrdemDeServicoCommandHandler : IRequestHandler<CancelarOrdemDeServicoCommand, SolicitacaoAceitaDto>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CancelarOrdemDeServicoCommandHandler(
        IOrdemDeServicoRepository ordemRepository,
        IIntegrationEventPublisher publisher,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _ordemRepository = ordemRepository;
        _publisher = publisher;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<SolicitacaoAceitaDto> Handle(CancelarOrdemDeServicoCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);
        ordem.ExigirCancelamentoAntesDaExecucao();

        // O cancelamento desfaz o que ja existe (orcamento, pagamento, producao, estoque) em servicos
        // diferentes, entao so a saga sabe o que compensar: aqui o status nao muda.
        await _publisher.PublishAsync(
            new CancelamentoSolicitado(Guid.NewGuid(), ordem.Id, _timeProvider.GetUtcNow(), request.Motivo.Trim()),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OrdemDeServicoMapper.ToSolicitacaoAceita(ordem);
    }
}
