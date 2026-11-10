using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.RecusarOrcamento;

public class RecusarOrcamentoCommandHandler : IRequestHandler<RecusarOrcamentoCommand, SolicitacaoAceitaDto>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RecusarOrcamentoCommandHandler(
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

    public async Task<SolicitacaoAceitaDto> Handle(RecusarOrcamentoCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);
        ordem.ExigirAguardandoDecisaoDoCliente();

        await _publisher.PublishAsync(
            new ClienteRecusouOrcamento(Guid.NewGuid(), ordem.Id, _timeProvider.GetUtcNow(), request.Motivo),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OrdemDeServicoMapper.ToSolicitacaoAceita(ordem);
    }
}
