using System.Net;
using FluentAssertions;
using OficinaMecanica.Bdd.Tests.Support;
using Reqnroll;

namespace OficinaMecanica.Bdd.Tests.Steps;

// Passos "Quando": o que o atendente e o cliente fazem pela API, o que o Billing e a Execucao publicam, e o tempo passando.
[Binding]
public sealed class AcoesSteps
{
    private readonly ContextoDoCenario _contexto;

    public AcoesSteps(ContextoDoCenario contexto)
    {
        _contexto = contexto;
    }

    // ---- Pela API ----

    [When("o atendente abre uma ordem de serviço com {int} unidade(s) do serviço {string} e {int} unidade(s) da peça {string}")]
    public Task AtendenteAbreAOrdem(int quantidadeDoServico, string servico, int quantidadeDaPeca, string peca) =>
        _contexto.AbrirOrdemAsync(servico, quantidadeDoServico, peca, quantidadeDaPeca);

    [When("o cliente aprova o orçamento")]
    public async Task ClienteAprova() =>
        (await _contexto.AprovarOrcamentoAsync()).Should().Be(HttpStatusCode.Accepted, "a aprovação é aceita e publicada para a saga");

    [When("o cliente tenta aprovar o orçamento")]
    public Task ClienteTentaAprovar() => _contexto.AprovarOrcamentoAsync();

    [When("o cliente recusa o orçamento com o motivo {string}")]
    public async Task ClienteRecusa(string motivo) =>
        (await _contexto.RecusarOrcamentoAsync(motivo)).Should().Be(HttpStatusCode.Accepted, "a recusa é aceita e publicada para a saga");

    [When("o atendente cancela a ordem de serviço com o motivo {string}")]
    public async Task AtendenteCancela(string motivo) =>
        (await _contexto.CancelarOrdemAsync(motivo)).Should().Be(HttpStatusCode.Accepted, "o cancelamento é aceito e publicado para a saga");

    // ---- Execucao ----

    [When("a Execução inicia o diagnóstico")]
    public Task ExecucaoIniciaODiagnostico() => _contexto.ExecucaoIniciaODiagnosticoAsync();

    [When("a Execução conclui o diagnóstico com o laudo {string}")]
    public Task ExecucaoConcluiODiagnostico(string laudo) => _contexto.ExecucaoConcluiODiagnosticoAsync(laudo);

    [When("a Execução informa que o reparo foi finalizado")]
    public Task ExecucaoFinalizaOReparo() => _contexto.ExecucaoFinalizaOReparoAsync();

    [When("a Execução informa que a execução é inviável com o motivo {string}")]
    public Task ExecucaoInviabilizaOReparo(string motivo) => _contexto.ExecucaoInviabilizaOReparoAsync(motivo);

    // ---- Billing ----

    [When("o Billing informa que o pagamento foi aprovado")]
    public Task BillingAprovaOPagamento() => _contexto.BillingAprovaOPagamentoAsync();

    [When("o Billing confirma o cancelamento do pagamento")]
    public Task BillingCancelaOPagamento() => _contexto.BillingCancelaOPagamentoAsync();

    [When("o Billing confirma o estorno do pagamento")]
    public Task BillingEstornaOPagamento() => _contexto.BillingEstornaOPagamentoAsync();

    // ---- Tempo ----

    [When("passam {int} horas e o sweeper de prazos roda")]
    public Task PassamHorasERodaOSweeper(int horas) => _contexto.AvancarORelogioERodarOSweeperAsync(horas);
}
