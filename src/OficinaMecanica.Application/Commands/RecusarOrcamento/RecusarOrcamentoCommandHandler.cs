using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.RecusarOrcamento;

public class RecusarOrcamentoCommandHandler : IRequestHandler<RecusarOrcamentoCommand, OrdemDeServicoDto>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecusarOrcamentoCommandHandler(
        IOrdemDeServicoRepository ordemRepository,
        IClienteRepository clienteRepository,
        IVeiculoRepository veiculoRepository,
        IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _clienteRepository = clienteRepository;
        _veiculoRepository = veiculoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrdemDeServicoDto> Handle(RecusarOrcamentoCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterPorIdAsync(request.OrdemDeServicoId, cancellationToken)
            ?? throw new DomainException("Ordem de serviço não encontrada.");

        ordem.RecusarOrcamento(request.Motivo);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var cliente = await _clienteRepository.ObterPorIdAsync(ordem.ClienteId, cancellationToken);
        var veiculo = await _veiculoRepository.ObterPorIdAsync(ordem.VeiculoId, cancellationToken);

        return OrdemDeServicoMapper.ToDto(ordem, cliente?.Nome, veiculo?.Placa.Valor);
    }
}
