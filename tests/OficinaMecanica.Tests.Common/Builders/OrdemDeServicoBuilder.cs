using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;

namespace OficinaMecanica.Tests.Common.Builders;

public class OrdemDeServicoBuilder
{
    private Guid _clienteId = Guid.NewGuid();
    private Guid _veiculoId = Guid.NewGuid();
    private string? _observacoes = null;
    private readonly List<(Guid PecaId, string Nome, decimal PrecoUnitario, int Quantidade)> _pecas = new();
    private bool _estoqueReservado;
    private bool _anteriorASaga;

    public OrdemDeServicoBuilder DoCliente(Guid clienteId) { _clienteId = clienteId; return this; }
    public OrdemDeServicoBuilder DoVeiculo(Guid veiculoId) { _veiculoId = veiculoId; return this; }
    public OrdemDeServicoBuilder ComObservacoes(string obs) { _observacoes = obs; return this; }

    // As pecas entram quando a OS chega a EmDiagnostico: depois disso o dominio nao aceita mais itens.
    public OrdemDeServicoBuilder ComPeca(Guid pecaId, string nome, decimal precoUnitario, int quantidade)
    {
        _pecas.Add((pecaId, nome, precoUnitario, quantidade));
        return this;
    }

    // Marca so o flag da OS (a reserva ja feita); o estoque da Peca correspondente e preparado pelo teste.
    public OrdemDeServicoBuilder ComEstoqueReservado() { _estoqueReservado = true; return this; }

    public OrdemDeServicoBuilder AnteriorASaga() { _anteriorASaga = true; return this; }

    public OrdemDeServico Recebida()
    {
        var o = new OrdemDeServico(_clienteId, _veiculoId, _observacoes);

        // Optei por reflexao: uma OS anterior a saga so existe como dado legado (default da coluna) e o
        // dominio nao oferece caminho nenhum para criar uma, de proposito.
        if (_anteriorASaga)
            typeof(OrdemDeServico).GetProperty(nameof(OrdemDeServico.ParticipaDaSaga))!.SetValue(o, false);

        return o;
    }

    public OrdemDeServico EmDiagnostico()
    {
        var o = Recebida();
        o.AvancarParaDiagnostico();
        foreach (var peca in _pecas)
            o.AdicionarPeca(peca.PecaId, peca.Nome, peca.PrecoUnitario, peca.Quantidade);
        return o;
    }

    public OrdemDeServico AguardandoAprovacao(string nomeServico = "Troca de Óleo", decimal preco = 100m)
    {
        var o = EmDiagnostico();
        o.AdicionarServico(Guid.NewGuid(), nomeServico, preco);
        o.EnviarParaAprovacao();

        if (_estoqueReservado)
            o.ReservarEstoque();

        return o;
    }

    public OrdemDeServico AguardandoPagamento(string nomeServico = "Troca de Óleo", decimal preco = 100m)
    {
        var o = AguardandoAprovacao(nomeServico, preco);
        o.AprovarOrcamento();
        return o;
    }

    public OrdemDeServico EmExecucao(string nomeServico = "Troca de Óleo", decimal preco = 100m)
    {
        var o = AguardandoPagamento(nomeServico, preco);
        o.IniciarExecucao();
        return o;
    }

    public OrdemDeServico OrcamentoRecusado(string nomeServico = "Troca de Óleo", decimal preco = 100m, string? motivo = null)
    {
        var o = AguardandoAprovacao(nomeServico, preco);
        o.RecusarOrcamento(motivo);
        return o;
    }

    public OrdemDeServico Cancelada(string nomeServico = "Troca de Óleo", decimal preco = 100m, string motivo = "Cancelamento solicitado pelo atendimento.")
    {
        var o = AguardandoAprovacao(nomeServico, preco);
        o.Cancelar(motivo);
        return o;
    }

    public OrdemDeServico Finalizada(string nomeServico = "Troca de Óleo", decimal preco = 100m)
    {
        var o = EmExecucao(nomeServico, preco);
        o.Finalizar();
        return o;
    }

    public OrdemDeServico Entregue(string nomeServico = "Troca de Óleo", decimal preco = 100m)
    {
        var o = Finalizada(nomeServico, preco);
        o.Entregar();
        return o;
    }

    public OrdemDeServico NoStatus(StatusOrdemDeServico status) => status switch
    {
        StatusOrdemDeServico.Recebida => Recebida(),
        StatusOrdemDeServico.EmDiagnostico => EmDiagnostico(),
        StatusOrdemDeServico.AguardandoAprovacao => AguardandoAprovacao(),
        StatusOrdemDeServico.AguardandoPagamento => AguardandoPagamento(),
        StatusOrdemDeServico.EmExecucao => EmExecucao(),
        StatusOrdemDeServico.Finalizada => Finalizada(),
        StatusOrdemDeServico.Entregue => Entregue(),
        StatusOrdemDeServico.OrcamentoRecusado => OrcamentoRecusado(),
        StatusOrdemDeServico.Cancelada => Cancelada(),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Status sem montagem no builder.")
    };
}
