using MediatR;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.RemoverCliente;

public class RemoverClienteCommandHandler : IRequestHandler<RemoverClienteCommand>
{
    private readonly IClienteRepository _clienteRepository;
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoverClienteCommandHandler(
        IClienteRepository clienteRepository,
        IOrdemDeServicoRepository ordemRepository,
        IUnitOfWork unitOfWork)
    {
        _clienteRepository = clienteRepository;
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RemoverClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await _clienteRepository.ObterPorIdAsync(request.Id, cancellationToken)
            ?? throw new DomainException("Cliente não encontrado.");

        var ordens = await _ordemRepository.ListarPorClienteAsync(request.Id, cancellationToken);
        if (ordens.Any())
            throw new DomainException("Não é possível remover um cliente que possui ordens de serviço.");

        _clienteRepository.Remover(cliente);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
