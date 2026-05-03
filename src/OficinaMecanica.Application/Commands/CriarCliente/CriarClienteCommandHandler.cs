using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Application.Commands.CriarCliente;

public class CriarClienteCommandHandler : IRequestHandler<CriarClienteCommand, ClienteDto>
{
    private readonly IClienteRepository _clienteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CriarClienteCommandHandler(IClienteRepository clienteRepository, IUnitOfWork unitOfWork)
    {
        _clienteRepository = clienteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ClienteDto> Handle(CriarClienteCommand request, CancellationToken cancellationToken)
    {
        var documento = Documento.Criar(request.Documento);

        var clienteExistente = await _clienteRepository.ObterPorDocumentoAsync(documento, cancellationToken);
        if (clienteExistente != null)
            throw new DomainException($"Já existe um cliente cadastrado com o documento {documento.Formatado}.");

        var cliente = new Cliente(request.Nome, documento, request.Email, request.Telefone);

        await _clienteRepository.AdicionarAsync(cliente, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ClienteDto(
            cliente.Id,
            cliente.Nome,
            cliente.Documento.Formatado,
            cliente.Documento.Tipo.ToString(),
            cliente.Email,
            cliente.Telefone,
            cliente.DataCadastro,
            Enumerable.Empty<VeiculoDto>()
        );
    }
}
