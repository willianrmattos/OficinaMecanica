using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.Entities;

public class OrdemDeServico : AggregateRoot
{
    public string Numero { get; private set; }
    public Guid ClienteId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public StatusOrdemDeServico Status { get; private set; }
    public decimal ValorTotal { get; private set; }
    public string? Observacoes { get; private set; }
    public DateTime DataAbertura { get; private set; }
    public DateTime? DataConclusao { get; private set; }

    private readonly List<ItemServico> _itensServico = new();
    public IReadOnlyCollection<ItemServico> ItensServico => _itensServico.AsReadOnly();

    private readonly List<ItemPeca> _itensPeca = new();
    public IReadOnlyCollection<ItemPeca> ItensPeca => _itensPeca.AsReadOnly();

    private readonly List<HistoricoStatus> _historicoStatus = new();
    public IReadOnlyCollection<HistoricoStatus> HistoricoStatus => _historicoStatus.AsReadOnly();

    private OrdemDeServico() { } // EF Core

    public OrdemDeServico(Guid clienteId, Guid veiculoId, string? observacoes)
    {
        Numero = GerarNumero();
        ClienteId = clienteId;
        VeiculoId = veiculoId;
        Status = StatusOrdemDeServico.Recebida;
        Observacoes = observacoes;
        DataAbertura = DateTime.UtcNow;
        ValorTotal = 0;

        _historicoStatus.Add(new HistoricoStatus(Id, StatusOrdemDeServico.Recebida, "Ordem de serviço criada."));
        AddDomainEvent(new OrdemDeServicoCriadaEvent(Id, Numero, clienteId));
    }

    public void AdicionarServico(Guid servicoId, string nomeServico, decimal preco, int quantidade = 1)
    {
        if (Status != StatusOrdemDeServico.Recebida && Status != StatusOrdemDeServico.EmDiagnostico)
            throw new DomainException("Só é possível adicionar serviços quando a OS está em 'Recebida' ou 'Em Diagnóstico'.");

        if (quantidade <= 0)
            throw new DomainException("A quantidade deve ser maior que zero.");

        var item = _itensServico.FirstOrDefault(i => i.ServicoId == servicoId);
        if (item != null)
            throw new DomainException($"O serviço '{nomeServico}' já foi adicionado a esta OS.");

        _itensServico.Add(new ItemServico(Id, servicoId, nomeServico, preco, quantidade));
        RecalcularValorTotal();
    }

    public void AdicionarPeca(Guid pecaId, string nomePeca, decimal precoUnitario, int quantidade)
    {
        if (Status != StatusOrdemDeServico.Recebida && Status != StatusOrdemDeServico.EmDiagnostico)
            throw new DomainException("Só é possível adicionar peças quando a OS está em 'Recebida' ou 'Em Diagnóstico'.");

        if (quantidade <= 0)
            throw new DomainException("A quantidade deve ser maior que zero.");

        var item = _itensPeca.FirstOrDefault(i => i.PecaId == pecaId);
        if (item != null)
        {
            item.AtualizarQuantidade(item.Quantidade + quantidade);
        }
        else
        {
            _itensPeca.Add(new ItemPeca(Id, pecaId, nomePeca, precoUnitario, quantidade));
        }

        RecalcularValorTotal();
    }

    public void AvancarParaDiagnostico()
    {
        ValidarTransicao(StatusOrdemDeServico.EmDiagnostico);
        AlterarStatus(StatusOrdemDeServico.EmDiagnostico, "Veículo em diagnóstico.");
    }

    public void EnviarParaAprovacao()
    {
        ValidarTransicao(StatusOrdemDeServico.AguardandoAprovacao);
        if (!_itensServico.Any())
            throw new DomainException("A OS deve ter pelo menos um serviço antes de enviar para aprovação.");

        AlterarStatus(StatusOrdemDeServico.AguardandoAprovacao, "Orçamento enviado para aprovação do cliente.");
        AddDomainEvent(new OrcamentoGeradoEvent(Id, Numero, ValorTotal));
    }

    public void AprovarOrcamento()
    {
        ValidarTransicao(StatusOrdemDeServico.EmExecucao);
        AlterarStatus(StatusOrdemDeServico.EmExecucao, "Orçamento aprovado pelo cliente. Serviço em execução.");
        AddDomainEvent(new OrcamentoAprovadoEvent(Id, Numero));
    }

    public void Finalizar()
    {
        ValidarTransicao(StatusOrdemDeServico.Finalizada);
        DataConclusao = DateTime.UtcNow;
        AlterarStatus(StatusOrdemDeServico.Finalizada, "Serviço finalizado.");
    }

    public void Entregar()
    {
        ValidarTransicao(StatusOrdemDeServico.Entregue);
        AlterarStatus(StatusOrdemDeServico.Entregue, "Veículo entregue ao cliente.");
    }

    private void AlterarStatus(StatusOrdemDeServico novoStatus, string observacao)
    {
        Status = novoStatus;
        _historicoStatus.Add(new HistoricoStatus(Id, novoStatus, observacao));
        AddDomainEvent(new StatusOrdemAlteradoEvent(Id, Numero, novoStatus));
    }

    private static readonly Dictionary<StatusOrdemDeServico, StatusOrdemDeServico[]> TransicoesPermitidas = new()
    {
        { StatusOrdemDeServico.Recebida, new[] { StatusOrdemDeServico.EmDiagnostico } },
        { StatusOrdemDeServico.EmDiagnostico, new[] { StatusOrdemDeServico.AguardandoAprovacao } },
        { StatusOrdemDeServico.AguardandoAprovacao, new[] { StatusOrdemDeServico.EmExecucao } },
        { StatusOrdemDeServico.EmExecucao, new[] { StatusOrdemDeServico.Finalizada } },
        { StatusOrdemDeServico.Finalizada, new[] { StatusOrdemDeServico.Entregue } },
    };

    private void ValidarTransicao(StatusOrdemDeServico novoStatus)
    {
        if (!TransicoesPermitidas.ContainsKey(Status) || !TransicoesPermitidas[Status].Contains(novoStatus))
            throw new DomainException($"Não é possível alterar o status de '{Status}' para '{novoStatus}'.");
    }

    private void RecalcularValorTotal()
    {
        var totalServicos = _itensServico.Sum(i => i.Subtotal);
        var totalPecas = _itensPeca.Sum(i => i.Subtotal);
        ValorTotal = totalServicos + totalPecas;
    }

    private static string GerarNumero()
    {
        return $"OS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
    }
}
