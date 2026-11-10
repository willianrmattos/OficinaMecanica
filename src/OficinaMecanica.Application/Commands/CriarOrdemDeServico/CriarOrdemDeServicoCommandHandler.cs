using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
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
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CriarOrdemDeServicoCommandHandler(
        IOrdemDeServicoRepository ordemRepository,
        IClienteRepository clienteRepository,
        IVeiculoRepository veiculoRepository,
        IServicoRepository servicoRepository,
        IPecaRepository pecaRepository,
        IIntegrationEventPublisher publisher,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _ordemRepository = ordemRepository;
        _clienteRepository = clienteRepository;
        _veiculoRepository = veiculoRepository;
        _servicoRepository = servicoRepository;
        _pecaRepository = pecaRepository;
        _publisher = publisher;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
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

                // So valida: o estoque so e reservado quando o cliente aprova o orcamento, senao uma
                // OS parada em diagnostico seguraria a peca por dias.
                if (peca.QuantidadeEstoque < itemPeca.Quantidade)
                    throw new DomainException($"Estoque insuficiente para a peça '{peca.Nome}'. Disponível: {peca.QuantidadeEstoque}.");

                ordem.AdicionarPeca(peca.Id, peca.Nome, peca.PrecoUnitario, itemPeca.Quantidade);
            }
        }

        await _ordemRepository.AdicionarAsync(ordem, cancellationToken);

        // Publica antes do SaveChanges: o outbox so grava a mensagem junto com a OS, no mesmo commit.
        await _publisher.PublishAsync(MontarMensagem(ordem, veiculo), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OrdemDeServicoMapper.ToDto(ordem, cliente.Nome, veiculo.Placa.Valor);
    }

    // A mensagem e um retrato da OS no momento da abertura: Billing e Execucao nunca consultam o banco daqui.
    private OrdemDeServicoAberta MontarMensagem(OrdemDeServico ordem, Veiculo veiculo) =>
        new(
            Guid.NewGuid(),
            ordem.Id,
            _timeProvider.GetUtcNow(),
            ordem.Numero,
            ordem.ClienteId,
            ordem.VeiculoId,
            veiculo.Placa.Valor,
            veiculo.Marca,
            veiculo.Modelo,
            ordem.Observacoes,
            ordem.ValorTotal,
            ordem.ItensServico
                .Select(i => new ItemDeServicoMensagem(i.ServicoId, i.NomeServico, i.PrecoUnitario, i.Quantidade))
                .ToList(),
            ordem.ItensPeca
                .Select(i => new ItemDePecaMensagem(i.PecaId, i.NomePeca, i.PrecoUnitario, i.Quantidade))
                .ToList());
}
