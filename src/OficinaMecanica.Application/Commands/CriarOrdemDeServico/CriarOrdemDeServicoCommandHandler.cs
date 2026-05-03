using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.CriarOrdemDeServico;

public class CriarOrdemDeServicoCommandHandler : IRequestHandler<CriarOrdemDeServicoCommand, OrdemDeServicoDto>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly IServicoRepository _servicoRepository;
    private readonly IPecaRepository _pecaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CriarOrdemDeServicoCommandHandler(
        IOrdemDeServicoRepository ordemRepository,
        IClienteRepository clienteRepository,
        IVeiculoRepository veiculoRepository,
        IServicoRepository servicoRepository,
        IPecaRepository pecaRepository,
        IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _clienteRepository = clienteRepository;
        _veiculoRepository = veiculoRepository;
        _servicoRepository = servicoRepository;
        _pecaRepository = pecaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrdemDeServicoDto> Handle(CriarOrdemDeServicoCommand request, CancellationToken cancellationToken)
    {
        var cliente = await _clienteRepository.ObterPorIdAsync(request.ClienteId, cancellationToken)
            ?? throw new DomainException("Cliente não encontrado.");

        var veiculo = await _veiculoRepository.ObterPorIdAsync(request.VeiculoId, cancellationToken)
            ?? throw new DomainException("Veículo não encontrado.");

        if (veiculo.ClienteId != cliente.Id)
            throw new DomainException("O veículo não pertence ao cliente informado.");

        var ordem = new OrdemDeServico(cliente.Id, veiculo.Id, request.Observacoes);

        foreach (var itemServico in request.Servicos)
        {
            var servico = await _servicoRepository.ObterPorIdAsync(itemServico.ServicoId, cancellationToken)
                ?? throw new DomainException($"Serviço com ID '{itemServico.ServicoId}' não encontrado.");

            if (!servico.Ativo)
                throw new DomainException($"O serviço '{servico.Nome}' está inativo.");

            ordem.AdicionarServico(servico.Id, servico.Nome, servico.Preco, itemServico.Quantidade);
        }

        if (request.Pecas != null)
        {
            foreach (var itemPeca in request.Pecas)
            {
                var peca = await _pecaRepository.ObterPorIdAsync(itemPeca.PecaId, cancellationToken)
                    ?? throw new DomainException($"Peça com ID '{itemPeca.PecaId}' não encontrada.");

                if (!peca.Ativo)
                    throw new DomainException($"A peça '{peca.Nome}' está inativa.");

                if (peca.QuantidadeEstoque < itemPeca.Quantidade)
                    throw new DomainException($"Estoque insuficiente para a peça '{peca.Nome}'. Disponível: {peca.QuantidadeEstoque}.");

                ordem.AdicionarPeca(peca.Id, peca.Nome, peca.PrecoUnitario, itemPeca.Quantidade);
            }
        }

        await _ordemRepository.AdicionarAsync(ordem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OrdemDeServicoMapper.ToDto(ordem, cliente.Nome, veiculo.Placa.Valor);
    }
}
