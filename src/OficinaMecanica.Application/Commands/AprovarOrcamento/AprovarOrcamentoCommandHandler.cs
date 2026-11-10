using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AprovarOrcamento;

public class AprovarOrcamentoCommandHandler : IRequestHandler<AprovarOrcamentoCommand, SolicitacaoAceitaDto>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AprovarOrcamentoCommandHandler(
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

    public async Task<SolicitacaoAceitaDto> Handle(AprovarOrcamentoCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);
        ordem.ExigirAguardandoDecisaoDoCliente();

        // A decisao so vira mensagem: quem muda o status e reserva o estoque e a saga, que serializa a
        // aprovacao contra o prazo. O SaveChanges continua necessario porque e ele que grava o outbox.
        await _publisher.PublishAsync(
            new ClienteAprovouOrcamento(Guid.NewGuid(), ordem.Id, _timeProvider.GetUtcNow()),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OrdemDeServicoMapper.ToSolicitacaoAceita(ordem);
    }
}
