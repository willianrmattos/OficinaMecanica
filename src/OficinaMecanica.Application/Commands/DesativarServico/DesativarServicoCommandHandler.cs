using MediatR;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.DesativarServico;

public class DesativarServicoCommandHandler : IRequestHandler<DesativarServicoCommand>
{
    private readonly IServicoRepository _servicoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DesativarServicoCommandHandler(IServicoRepository servicoRepository, IUnitOfWork unitOfWork)
    {
        _servicoRepository = servicoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DesativarServicoCommand request, CancellationToken cancellationToken)
    {
        var servico = await _servicoRepository.ObterPorIdAsync(request.Id, cancellationToken)
            ?? throw new DomainException("Serviço não encontrado.");

        if (request.Ativar)
            servico.Ativar();
        else
            servico.Desativar();

        _servicoRepository.Atualizar(servico);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
