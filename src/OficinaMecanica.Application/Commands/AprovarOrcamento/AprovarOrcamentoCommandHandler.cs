using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.AprovarOrcamento;

public class AprovarOrcamentoCommandHandler : IRequestHandler<AprovarOrcamentoCommand, OrdemDeServicoDto>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly IPecaRepository _pecaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AprovarOrcamentoCommandHandler(
        IOrdemDeServicoRepository ordemRepository,
        IClienteRepository clienteRepository,
        IVeiculoRepository veiculoRepository,
        IPecaRepository pecaRepository,
        IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _clienteRepository = clienteRepository;
        _veiculoRepository = veiculoRepository;
        _pecaRepository = pecaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrdemDeServicoDto> Handle(AprovarOrcamentoCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterPorIdAsync(request.OrdemDeServicoId, cancellationToken)
            ?? throw new DomainException("Ordem de serviço não encontrada.");

        ordem.AprovarOrcamento();

        foreach (var item in ordem.ItensPeca)
        {
            var peca = await _pecaRepository.ObterPorIdAsync(item.PecaId, cancellationToken)
                ?? throw new DomainException($"Peça '{item.NomePeca}' não encontrada no sistema.");
            peca.RemoverEstoque(item.Quantidade);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var cliente = await _clienteRepository.ObterPorIdAsync(ordem.ClienteId, cancellationToken);
        var veiculo = await _veiculoRepository.ObterPorIdAsync(ordem.VeiculoId, cancellationToken);

        return OrdemDeServicoMapper.ToDto(ordem, cliente?.Nome, veiculo?.Placa.Valor);
    }
}
