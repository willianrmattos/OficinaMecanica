using OficinaMecanica.Bdd.Tests.Support;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Tests.Common.Builders;
using Reqnroll;

namespace OficinaMecanica.Bdd.Tests.Steps;

// Passos "Dado": a oficina, os servicos externos programados e as ordens ja conduzidas ate uma etapa.
[Binding]
public sealed class PreparacaoSteps
{
    private readonly ContextoDoCenario _contexto;

    public PreparacaoSteps(ContextoDoCenario contexto)
    {
        _contexto = contexto;
    }

    [Given("uma oficina com o serviço {string} de {int} reais")]
    public Task OficinaComServico(string nome, int preco) => _contexto.CadastrarServicoAsync(nome, preco);

    [Given("a peça {string} de {int} reais com {int} unidade(s) em estoque")]
    public Task PecaEmEstoque(string nome, int preco, int estoque) => _contexto.CadastrarPecaAsync(nome, preco, estoque);

    [Given("o cliente {string} com um veículo de placa {string}")]
    public Task ClienteComVeiculo(string nome, string placa) => _contexto.CadastrarClienteComVeiculoAsync(nome, placa);

    // ---- Billing e Execucao programados ----

    [Given("o Billing gera o orçamento e o link de pagamento assim que recebe o pedido")]
    public void BillingCooperativo() => _contexto.ProgramarBillingCooperativo();

    [Given("o Billing não consegue estornar pagamentos")]
    public void BillingNaoEstorna() =>
        _contexto.Roteiro.AoReceber<EstornarPagamento>(pedido => new EstornoFalhouBuilder()
            .DaOrdem(pedido.OrdemDeServicoId)
            .ComPagamento(pedido.PagamentoId)
            .ComErro("O gateway de pagamento está indisponível.")
            .ComOcorridoEm(_contexto.Fabrica.Relogio.GetUtcNow())
            .Build());

    [Given("o Billing já recebeu o pagamento quando o pedido de cancelamento chega")]
    public void BillingJaRecebeuOPagamento() =>
        _contexto.Roteiro.AoReceber<CancelarPagamento>(pedido => new PagamentoAprovadoBuilder()
            .DaOrdem(pedido.OrdemDeServicoId)
            .ComPagamento(_contexto.PagamentoId)
            .ComValor(_contexto.ValorDoOrcamento)
            .ComOcorridoEm(_contexto.Fabrica.Relogio.GetUtcNow())
            .Build());

    [Given("o Billing falha tecnicamente ao cancelar pagamentos")]
    public void BillingFalhaAoCancelar() =>
        _contexto.Roteiro.FalharAoReceber<CancelarPagamento>("O Billing está indisponível.");

    // ---- Ordens conduzidas pelo caminho real (HTTP + eventos), nunca por atalho no banco ----

    [Given("uma ordem de serviço com {int} unidade(s) da peça {string} e o orçamento aguardando a decisão do cliente")]
    public Task OrdemAguardandoAprovacao(int quantidade, string peca) =>
        _contexto.ConduzirAteAguardandoAprovacaoAsync(_contexto.Servicos.Keys.Single(), peca, quantidade);

    [Given("uma ordem de serviço com {int} unidade(s) da peça {string} aprovada e aguardando o pagamento")]
    public Task OrdemAguardandoPagamento(int quantidade, string peca) =>
        _contexto.ConduzirAteAguardandoPagamentoAsync(_contexto.Servicos.Keys.Single(), peca, quantidade);

    [Given("uma ordem de serviço com {int} unidade(s) da peça {string} paga e em execução")]
    public Task OrdemEmExecucao(int quantidade, string peca) =>
        _contexto.ConduzirAteEmExecucaoAsync(_contexto.Servicos.Keys.Single(), peca, quantidade);

    [Given("a peça {string} ficou com apenas {int} unidade(s) em estoque depois da abertura da ordem")]
    public Task EstoqueReduzido(string peca, int quantidade) => _contexto.ReduzirEstoqueDaPecaParaAsync(peca, quantidade);
}
