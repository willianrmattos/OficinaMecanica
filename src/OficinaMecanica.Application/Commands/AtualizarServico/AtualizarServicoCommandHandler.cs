using MediatR;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AtualizarServico;

public class AtualizarServicoCommandHandler : IRequestHandler<AtualizarServicoCommand>
{
    private readonly IServicoRepository _servicoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AtualizarServicoCommandHandler(IServicoRepository servicoRepository, IUnitOfWork unitOfWork)
    {
        _servicoRepository = servicoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AtualizarServicoCommand request, CancellationToken cancellationToken)
    {
        var servico = await _servicoRepository.ObterPorIdAsync(request.Id, cancellationToken)
            ?? throw new DomainException("Serviço não encontrado.");

        servico.Atualizar(request.Nome, request.Descricao, request.Preco, request.TempoEstimadoMinutos);

        _servicoRepository.Atualizar(servico);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
