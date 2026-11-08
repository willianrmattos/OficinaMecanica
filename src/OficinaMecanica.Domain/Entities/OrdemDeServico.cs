using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.Entities;

public class OrdemDeServico : AggregateRoot
{
    // O motivo tambem vai para HistoricoStatus.Observacao (500 caracteres, junto do prefixo da frase),
    // entao o limite do motivo precisa deixar folga para ele.
    public const int TamanhoMaximoDoMotivo = 400;
    public const int TamanhoMaximoDoLinkDePagamento = 2000;

    public string Numero { get; private set; }
    public Guid ClienteId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public StatusOrdemDeServico Status { get; private set; }
    public decimal ValorTotal { get; private set; }
    public string? Observacoes { get; private set; }
    public DateTime DataAbertura { get; private set; }
    public DateTime? DataConclusao { get; private set; }

    // OS anteriores a saga ficam false (default da coluna): so as criadas por este construtor participam
    // dela, e e isso que separa uma decisao aceita de um 409 em aprovar/recusar/cancelar.
    public bool ParticipaDaSaga { get; private set; }
    public bool EstoqueReservado { get; private set; }
    public Guid? PagamentoId { get; private set; }
    public string? LinkPagamento { get; private set; }
    public DateTimeOffset? PagamentoExpiraEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    public bool AguardaDecisaoDoCliente => Status == StatusOrdemDeServico.AguardandoAprovacao;

    public bool PodeSerCanceladaAntesDaExecucao =>
        Status is StatusOrdemDeServico.Recebida
            or StatusOrdemDeServico.EmDiagnostico
            or StatusOrdemDeServico.AguardandoAprovacao
            or StatusOrdemDeServico.AguardandoPagamento;

    private readonly List<ItemServico> _itensServico = new();
    public IReadOnlyCollection<ItemServico> ItensServico => _itensServico.AsReadOnly();

    private readonly List<ItemPeca> _itensPeca = new();
    public IReadOnlyCollection<ItemPeca> ItensPeca => _itensPeca.AsReadOnly();

    private readonly List<HistoricoStatus> _historicoStatus = new();

    // A ordem cronologica e regra do agregado, nao da consulta: o banco entrega as linhas na ordem do indice da chave, que e um Guid
    // aleatorio, e o EF preenche a lista nessa ordem. Em empate de data (duas transicoes no mesmo instante) optei por desempatar pela
    // posicao do status no fluxo, e nao por uma chave sequencial (pediria migration, e o Guid nao serve): as transicoes so avancam e
    // cada status entra uma unica vez, entao a posicao reproduz a ordem em que ocorreram.
    public IReadOnlyCollection<HistoricoStatus> HistoricoStatus =>
        _historicoStatus
            .OrderBy(h => h.DataAlteracao)
            .ThenBy(h => PosicaoNoFluxo(h.Status))
            .ToList()
            .AsReadOnly();

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
        ParticipaDaSaga = true;

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
        ValidarTransicao(StatusOrdemDeServico.AguardandoPagamento);
        AlterarStatus(StatusOrdemDeServico.AguardandoPagamento, "Orçamento aprovado pelo cliente. Aguardando pagamento.");
        AddDomainEvent(new OrcamentoAprovadoEvent(Id, Numero));
    }

    public void RecusarOrcamento(string? motivo = null)
    {
        ValidarTransicao(StatusOrdemDeServico.OrcamentoRecusado);
        var observacao = string.IsNullOrWhiteSpace(motivo)
            ? "Orçamento recusado pelo cliente."
            : $"Orçamento recusado pelo cliente. Motivo: {motivo}";
        AlterarStatus(StatusOrdemDeServico.OrcamentoRecusado, observacao);
        AddDomainEvent(new OrcamentoRecusadoEvent(Id, Numero, motivo));
    }

    public void IniciarExecucao()
    {
        ValidarTransicao(StatusOrdemDeServico.EmExecucao);
        AlterarStatus(StatusOrdemDeServico.EmExecucao, "Pagamento confirmado. Serviço em execução.");
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

    public void RegistrarPagamentoSolicitado(Guid pagamentoId, string linkPagamento, DateTimeOffset expiraEm)
    {
        if (pagamentoId == Guid.Empty)
            throw new DomainException("O identificador do pagamento é obrigatório.");
        if (string.IsNullOrWhiteSpace(linkPagamento))
            throw new DomainException("O link de pagamento é obrigatório.");
        if (linkPagamento.Length > TamanhoMaximoDoLinkDePagamento)
            throw new DomainException($"O link de pagamento deve ter no máximo {TamanhoMaximoDoLinkDePagamento} caracteres.");

        // Reentrega da mesma mensagem: ja registrado, mesmo que a OS tenha avancado de status desde entao.
        if (PagamentoId == pagamentoId)
            return;

        if (Status != StatusOrdemDeServico.AguardandoPagamento)
            throw new DomainException($"Só é possível registrar o pagamento quando a OS está em 'AguardandoPagamento'. Status atual: '{Status}'.");
        if (PagamentoId is not null)
            throw new DomainException("A OS já possui outro pagamento registrado.");

        PagamentoId = pagamentoId;
        LinkPagamento = linkPagamento;
        PagamentoExpiraEm = expiraEm;
        AddDomainEvent(new PagamentoSolicitadoEvent(Id, Numero, ClienteId, linkPagamento, expiraEm));
    }

    // Separado de ReservarEstoque para a Application poder checar o status antes de olhar o estoque das pecas,
    // sem alterar nada na OS.
    public void ValidarPodeReservarEstoque()
    {
        if (Status is not (StatusOrdemDeServico.AguardandoAprovacao or StatusOrdemDeServico.AguardandoPagamento))
            throw new DomainException("Só é possível reservar o estoque quando a OS está em 'AguardandoAprovacao' ou 'AguardandoPagamento'.");
    }

    // So marca o flag: a movimentacao real do estoque e da Application, que enxerga as pecas.
    public void ReservarEstoque()
    {
        if (EstoqueReservado)
            return;

        ValidarPodeReservarEstoque();
        EstoqueReservado = true;
    }

    public void LiberarEstoque() => EstoqueReservado = false;

    public void Cancelar(string motivo)
    {
        if (Status == StatusOrdemDeServico.Cancelada)
            return;

        if (string.IsNullOrWhiteSpace(motivo))
            throw new DomainException("O motivo do cancelamento é obrigatório.");

        motivo = motivo.Trim();
        if (motivo.Length > TamanhoMaximoDoMotivo)
            throw new DomainException($"O motivo do cancelamento deve ter no máximo {TamanhoMaximoDoMotivo} caracteres.");

        ValidarTransicao(StatusOrdemDeServico.Cancelada);
        MotivoCancelamento = motivo;
        AlterarStatus(StatusOrdemDeServico.Cancelada, $"Ordem de serviço cancelada. Motivo: {motivo}");
    }

    // Um comando repetido ou atrasado nao pode falhar quando a OS ja chegou ao status alvo (ou passou dele
    // no caminho principal): e isso que torna idempotentes os comandos que a saga reenvia.
    // Cancelada e OrcamentoRecusado ficam fora do caminho, entao nunca contam como "ja alcancou".
    public bool JaAlcancou(StatusOrdemDeServico alvo)
    {
        var posicaoAtual = Array.IndexOf(CaminhoPrincipal, Status);
        var posicaoAlvo = Array.IndexOf(CaminhoPrincipal, alvo);
        return posicaoAtual >= 0 && posicaoAlvo >= 0 && posicaoAtual >= posicaoAlvo;
    }

    // Cancelada e OrcamentoRecusado saem do caminho principal e sao terminais (nunca coexistem), entao ficam depois de todos os demais.
    public static int PosicaoNoFluxo(StatusOrdemDeServico status)
    {
        var posicao = Array.IndexOf(CaminhoPrincipal, status);
        return posicao >= 0 ? posicao : CaminhoPrincipal.Length;
    }

    private void AlterarStatus(StatusOrdemDeServico novoStatus, string observacao)
    {
        Status = novoStatus;
        _historicoStatus.Add(new HistoricoStatus(Id, novoStatus, observacao));
        AddDomainEvent(new StatusOrdemAlteradoEvent(Id, Numero, novoStatus));
    }

    private static readonly StatusOrdemDeServico[] CaminhoPrincipal =
    {
        StatusOrdemDeServico.Recebida,
        StatusOrdemDeServico.EmDiagnostico,
        StatusOrdemDeServico.AguardandoAprovacao,
        StatusOrdemDeServico.AguardandoPagamento,
        StatusOrdemDeServico.EmExecucao,
        StatusOrdemDeServico.Finalizada,
        StatusOrdemDeServico.Entregue
    };

    private static readonly Dictionary<StatusOrdemDeServico, StatusOrdemDeServico[]> TransicoesPermitidas = new()
    {
        { StatusOrdemDeServico.Recebida, new[] { StatusOrdemDeServico.EmDiagnostico, StatusOrdemDeServico.Cancelada } },
        { StatusOrdemDeServico.EmDiagnostico, new[] { StatusOrdemDeServico.AguardandoAprovacao, StatusOrdemDeServico.Cancelada } },
        { StatusOrdemDeServico.AguardandoAprovacao, new[] { StatusOrdemDeServico.AguardandoPagamento, StatusOrdemDeServico.OrcamentoRecusado, StatusOrdemDeServico.Cancelada } },
        { StatusOrdemDeServico.AguardandoPagamento, new[] { StatusOrdemDeServico.EmExecucao, StatusOrdemDeServico.Cancelada } },
        // EmExecucao -> Cancelada so existe como compensacao (execucao inviavel, estorno): nenhuma rota HTTP chega aqui.
        { StatusOrdemDeServico.EmExecucao, new[] { StatusOrdemDeServico.Finalizada, StatusOrdemDeServico.Cancelada } },
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
