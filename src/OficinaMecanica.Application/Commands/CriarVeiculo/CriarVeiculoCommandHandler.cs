using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Application.Commands.CriarVeiculo;

public class CriarVeiculoCommandHandler : IRequestHandler<CriarVeiculoCommand, VeiculoDto>
{
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CriarVeiculoCommandHandler(
        IVeiculoRepository veiculoRepository,
        IClienteRepository clienteRepository,
        IUnitOfWork unitOfWork)
    {
        _veiculoRepository = veiculoRepository;
        _clienteRepository = clienteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<VeiculoDto> Handle(CriarVeiculoCommand request, CancellationToken cancellationToken)
    {
        var cliente = await _clienteRepository.ObterPorIdAsync(request.ClienteId, cancellationToken)
            ?? throw new DomainException("Cliente não encontrado.");

        var placa = Placa.Criar(request.Placa);

        var veiculoExistente = await _veiculoRepository.ObterPorPlacaAsync(placa, cancellationToken);
        if (veiculoExistente != null)
            throw new DomainException($"Já existe um veículo cadastrado com a placa {placa.Valor}.");

        var veiculo = new Veiculo(placa, request.Marca, request.Modelo, request.Ano, cliente.Id);

        await _veiculoRepository.AdicionarAsync(veiculo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VeiculoDto(veiculo.Id, veiculo.Placa.Valor, veiculo.Marca, veiculo.Modelo, veiculo.Ano, veiculo.ClienteId);
    }
}
