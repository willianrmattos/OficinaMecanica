using MediatR;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AtualizarCliente;

public class AtualizarClienteCommandHandler : IRequestHandler<AtualizarClienteCommand>
{
    private readonly IClienteRepository _clienteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AtualizarClienteCommandHandler(IClienteRepository clienteRepository, IUnitOfWork unitOfWork)
    {
        _clienteRepository = clienteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AtualizarClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await _clienteRepository.ObterPorIdAsync(request.Id, cancellationToken)
            ?? throw new DomainException("Cliente não encontrado.");

        cliente.Atualizar(request.Nome, request.Email, request.Telefone);

        _clienteRepository.Atualizar(cliente);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
