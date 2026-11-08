using System.Net;
using FluentAssertions;
using OficinaMecanica.Bdd.Tests.Support;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Common;
using Reqnroll;

namespace OficinaMecanica.Bdd.Tests.Steps;

// Passos "Entao": estado da saga, da OS, do estoque e das mensagens. Todo passo que depende da saga ou de um servico
// responder espera com teto de 10 s e reprova dizendo o que esperava e o que viu.
[Binding]
public sealed class VerificacoesSteps
{
    private readonly ContextoDoCenario _contexto;

    public VerificacoesSteps(ContextoDoCenario contexto)
    {
        _contexto = contexto;
    }

    // ---- Saga e ordem de servico ----

    [Then("a saga da ordem está em {string}")]
    public Task SagaEm(string estado) => _contexto.AguardarSagaEmAsync(estado);

    // Sem espera: usado depois de um passo que ja esperou o evento relevante, para provar que a saga NAO saiu do estado.
    [Then("a saga da ordem continua em {string}")]
    public void SagaContinuaEm(string estado)
    {
        var saga = _contexto.SagaDaOrdem();
        saga.Should().NotBeNull("a saga da ordem deveria existir");
        saga!.CurrentState.Should().Be(estado);
    }

    [Then("a ordem de serviço está com o status {string}")]
    public Task OrdemComStatus(string status) =>
        Esperar.AteAsync(
            async () => (await _contexto.ConsultarOrdemAsync()).GetProperty("status").GetString(),
            atual => atual == status,
            $"a ordem de serviço com o status '{status}'");

    [Then("o motivo do cancelamento da ordem contém {string}")]
    public Task MotivoDoCancelamento(string trecho) =>
        Esperar.AteAsync(
            async () => (await _contexto.OrdemNoBancoAsync()).MotivoCancelamento,
            motivo => motivo is not null && motivo.Contains(trecho),
            $"o motivo do cancelamento da ordem contendo '{trecho}'");

    [Then("a ordem de serviço tem o link de pagamento")]
    public async Task OrdemTemLinkDePagamento()
    {
        var link = await _contexto.AguardarLinkDePagamentoAsync();
        link.Should().Be(_contexto.LinkDePagamento);
    }

    [Then("a reserva de estoque da ordem está ativa")]
    public Task ReservaAtiva() => EsperarReservaAsync(ativa: true);

    [Then("a reserva de estoque da ordem foi liberada")]
    public Task ReservaLiberada() => EsperarReservaAsync(ativa: false);

    // ---- Estoque ----

    [Then("o estoque da peça {string} é de {int} unidade(s)")]
    public Task EstoqueDaPeca(string peca, int esperado) =>
        Esperar.AteAsync(() => _contexto.EstoqueDaPecaAsync(peca), atual => atual == esperado, $"o estoque da peça '{peca}' em {esperado} unidade(s)");

    // ---- Mensagens: o que o servico recebeu na fila dele ----

    [Then("o Billing recebeu o comando {string}")]
    public Task BillingRecebeu(string comando) => EsperarRecebimentoAsync(BillingSimulado.Nome, comando, vezes: null);

    [Then("o Billing recebeu o comando {string} {int} vez(es)")]
    public Task BillingRecebeuNVezes(string comando, int vezes) => EsperarRecebimentoAsync(BillingSimulado.Nome, comando, vezes);

    [Then("a Execução recebeu o comando {string}")]
    public Task ExecucaoRecebeu(string comando) => EsperarRecebimentoAsync(ExecucaoSimulada.Nome, comando, vezes: null);

    [Then("a Execução recebeu o comando {string} {int} vez(es)")]
    public Task ExecucaoRecebeuNVezes(string comando, int vezes) => EsperarRecebimentoAsync(ExecucaoSimulada.Nome, comando, vezes);

    // Verificacao de ausencia olha o que a SAGA enviou, que e registrado no envio: se olhasse o que o servico recebeu, ela
    // passaria so porque o consumer ainda nao leu uma mensagem enviada por engano.
    [Then("a saga não enviou o comando {string}")]
    public void SagaNaoEnviou(string comando) =>
        _contexto.ComandosEnviadosPelaSaga().Should().NotContain(comando, $"a saga não deveria ter enviado {comando}");

    [Then("o Billing recebeu o orçamento da ordem com o valor total de {int} reais")]
    public async Task BillingRecebeuOPedidoDeOrcamento(int valor)
    {
        await EsperarRecebimentoAsync(BillingSimulado.Nome, nameof(GerarOrcamento), vezes: 1);
        var pedido = _contexto.Roteiro.RecebidosDoTipo<GerarOrcamento>().Single();
        pedido.OrdemDeServicoId.Should().Be(_contexto.OrdemId);
        pedido.ValorTotal.Should().Be(valor);
    }

    [Then("o Billing recebeu o pedido de estorno do pagamento da ordem")]
    public async Task BillingRecebeuOPedidoDeEstorno()
    {
        await EsperarRecebimentoAsync(BillingSimulado.Nome, nameof(EstornarPagamento), vezes: 1);
        var pedido = _contexto.Roteiro.RecebidosDoTipo<EstornarPagamento>().Single();
        pedido.OrdemDeServicoId.Should().Be(_contexto.OrdemId);
        pedido.PagamentoId.Should().Be(_contexto.PagamentoId, "o estorno é do pagamento que o Billing aprovou");
    }

    [Then("o Billing recebeu o pedido de cancelamento do orçamento com o motivo contendo {string}")]
    public async Task BillingRecebeuOCancelamentoDoOrcamento(string trecho)
    {
        await EsperarRecebimentoAsync(BillingSimulado.Nome, nameof(CancelarOrcamento), vezes: 1);
        var pedido = _contexto.Roteiro.RecebidosDoTipo<CancelarOrcamento>().Single();
        pedido.OrdemDeServicoId.Should().Be(_contexto.OrdemId);
        pedido.OrcamentoId.Should().Be(_contexto.OrcamentoId);
        pedido.Motivo.Should().Contain(trecho);
    }

    // O Fault<T> e o sinal de falha tecnica do servico de destino; a saga so o trata como compensacao presa se o consumir.
    [Then("a saga recebeu a falha técnica do comando {string}")]
    public Task SagaRecebeuAFalha(string comando) => comando switch
    {
        nameof(CancelarPagamento) => EsperarFalhaAsync<CancelarPagamento>(),
        nameof(CancelarOrcamento) => EsperarFalhaAsync<CancelarOrcamento>(),
        nameof(CancelarProducao) => EsperarFalhaAsync<CancelarProducao>(),
        _ => throw new ArgumentException($"O cenário não sabe esperar a falha do comando {comando}.")
    };

    // ---- Notificacao, HTTP e sweeper ----

    [Then("o cliente recebeu um e-mail com o assunto contendo {string}")]
    public async Task ClienteRecebeuEmail(string trecho)
    {
        var emails = await Esperar.AteQue(
            () => _contexto.Emails.Enviados,
            enviados => enviados.Any(e => e.Assunto.Contains(trecho)),
            $"um e-mail com o assunto contendo '{trecho}'",
            enviados => string.Join(" | ", enviados.Select(e => e.Assunto)));

        emails.Where(e => e.Assunto.Contains(trecho)).Should().OnlyContain(e => e.Destinatario == _contexto.Cliente!.Email);
    }

    [Then("a API respondeu {int}")]
    public void ApiRespondeu(int status) => ((int)_contexto.UltimoStatusHttp!.Value).Should().Be(status);

    [Then("o sweeper publicou {int} prazo(s) vencido(s)")]
    public void SweeperPublicou(int quantidade) => _contexto.PrazosPublicadosPeloSweeper.Should().Be(quantidade);

    [Then("o prazo de {word} da ordem foi marcado como processado")]
    public async Task PrazoProcessado(string tipo)
    {
        var tipoDoPrazo = Enum.Parse<TipoPrazo>(tipo);
        await Esperar.AteAsync(
            async () => (await _contexto.PrazosDaOrdemAsync()).Where(p => p.Tipo == tipoDoPrazo).ToList(),
            prazos => prazos.Count > 0 && prazos.All(p => p.Processado),
            $"o prazo de {tipo} da ordem marcado como processado",
            prazos => string.Join(", ", prazos.Select(p => $"{p.Tipo}:{(p.Processado ? "processado" : "pendente")}")));
    }

    // ---- Metricas da saga (System.Diagnostics.Metrics): o que a operacao veria no OpenTelemetry ----

    [Then("a métrica {string} foi incrementada {int} vez(es)")]
    public Task MetricaIncrementada(string metrica, int vezes) => EsperarMetricaAsync(metrica, null, null, vezes);

    [Then("a métrica {string} com a tag {string} igual a {string} foi incrementada {int} vez(es)")]
    public Task MetricaComTagIncrementada(string metrica, string tag, string valor, int vezes) => EsperarMetricaAsync(metrica, tag, valor, vezes);

    [Then("a métrica {string} não foi incrementada")]
    public void MetricaNaoIncrementada(string metrica) =>
        _contexto.Metricas.Somar(metrica).Should().Be(0, $"a métrica {metrica} não deveria ter sido emitida");

    // A metrica sai logo depois de a saga mudar de estado, entao o passo espera ate somar o esperado e so depois afirma o valor
    // exato (uma emissao a mais tambem reprova).
    private async Task EsperarMetricaAsync(string metrica, string? tag, string? valor, int vezes)
    {
        var descricao = tag is null ? metrica : $"{metrica} com {tag}={valor}";
        var somado = await Esperar.AteQue(
            () => _contexto.Metricas.Somar(metrica, tag, valor),
            soma => soma >= vezes,
            $"a métrica {descricao} somar {vezes}");

        somado.Should().Be(vezes, $"a métrica {descricao} deveria somar exatamente {vezes}");
    }

    private Task EsperarReservaAsync(bool ativa) =>
        Esperar.AteAsync(
            async () => (await _contexto.OrdemNoBancoAsync()).EstoqueReservado,
            reservada => reservada == ativa,
            ativa ? "a reserva de estoque da ordem ativa" : "a reserva de estoque da ordem liberada");

    private Task EsperarFalhaAsync<TComando>() where TComando : class =>
        Esperar.AteQue(
            () => _contexto.SagaConsumiuFalhaDe<TComando>(),
            consumiu => consumiu,
            $"a saga consumir Fault<{typeof(TComando).Name}>");

    private async Task EsperarRecebimentoAsync(string servico, string comando, int? vezes)
    {
        var descricao = vezes is null
            ? $"o {servico} receber o comando {comando}"
            : $"o {servico} receber o comando {comando} {vezes} vez(es)";

        await Esperar.AteQue(
            () => _contexto.Roteiro.Recebidos.Where(r => r.Servico == servico && r.Nome == comando).Count(),
            quantidade => vezes is null ? quantidade >= 1 : quantidade == vezes,
            descricao);

        if (vezes is not null)
        {
            // Nao basta chegar ao numero: um comando duplicado que chegasse depois nao pode passar despercebido.
            var enviados = _contexto.ComandosEnviadosPelaSaga().Count(nome => nome == comando);
            enviados.Should().Be(vezes.Value, $"a saga deveria ter enviado {comando} exatamente {vezes} vez(es)");
        }
    }
}
