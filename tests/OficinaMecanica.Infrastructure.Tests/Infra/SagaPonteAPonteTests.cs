using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.Commands.CancelarOrdemDeServico;
using OficinaMecanica.Application.Commands.RecusarOrcamento;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// A saga ponta a ponta contra SQL Server e RabbitMQ de verdade: a OS nasce e e decidida pelos commands da Application (outbox
// transacional), a saga a conduz pelo broker, e Billing e Execucao (consumers de teste nas filas reais) respondem com os
// contratos. Em todo desfecho: nada em _error, estoque coerente e saga e OS no mesmo estado. A excecao sao os testes de Fault
// real, em que o consumer do Billing lanca de proposito: o comando que falhou fica na _error do destino (e o que o redrive
// devolve) e o Fault<T> chega na saga pelo broker, o caminho que um Fault montado a mao no bus em memoria nao prova. Os
// endpoints do Billing e da Execucao daqui nao tem retry (o Fault sai na primeira falha); o retry do endpoint de destino e
// provado em RetryEFilaDeErroTests.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class SagaPonteAPonteTests
{
    private const int EstoqueDepoisDaReserva = AmbienteDaSagaInfra.EstoqueInicial - AmbienteDaSagaInfra.QuantidadeDaPeca;

    private readonly InfraFixture _infra;

    public SagaPonteAPonteTests(InfraFixture infra) => _infra = infra;

    private Task<AmbienteDaSagaInfra> CriarAsync() => AmbienteDaSagaInfra.CriarAsync(_infra);

    private static async Task ChegarEmAguardandoPagamentoAsync(AmbienteDaSagaInfra a, AmbienteDaSagaInfra.OrdemCriada ordem)
    {
        await a.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");
        await a.AprovarAsync(ordem.OrdemId);
        await a.AguardarEstadoAsync(ordem.OrdemId, "AguardandoPagamento");
        (await Esperar.AteAsync(async () => (await a.OrdemAsync(ordem.OrdemId)).PagamentoId != null, TimeSpan.FromSeconds(30)))
            .Should().BeTrue("o Billing responde PagamentoSolicitado com o link");
    }

    [Fact]
    public async Task FilaDaSaga_DeveExistirAoSubirOBus_ESolicitarDiagnosticoChegaAExecucaoSemPerderOrdemDeServicoAberta()
    {
        await using var ambiente = await CriarAsync();

        var filas = await _infra.FilasAsync(ambiente.Infra.VirtualHost);
        filas.Should().Contain(new[] { Filas.Saga, Filas.Billing, Filas.Execucao }, "a fila da saga nasce com o bus, antes de qualquer trafego");

        var ordem = await ambiente.CriarOrdemAsync();

        (await Esperar.AteAsync(() => ambiente.Servicos.NaExecucao<SolicitarDiagnostico>(ordem.OrdemId) == 1)).Should().BeTrue();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");
    }

    [Fact]
    public async Task CaminhoFeliz_DeveIrDaAberturaAteConcluidaComEstoqueConsumidoEUmEmailPorTransicao()
    {
        await using var ambiente = await CriarAsync();

        var ordem = await ambiente.CriarOrdemAsync();
        await ChegarEmAguardandoPagamentoAsync(ambiente, ordem);
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(EstoqueDepoisDaReserva);
        var pagamentoId = ComportamentoDosServicos.PagamentoDa(ordem.OrdemId);

        await ambiente.PublicarAsync(new PagamentoAprovadoBuilder().DaOrdem(ordem.OrdemId).ComPagamento(pagamentoId).ComValor(270m).Build());
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "EmExecucao");
        (await Esperar.AteAsync(() => ambiente.Servicos.NaExecucao<IniciarExecucao>(ordem.OrdemId) == 1)).Should().BeTrue();

        await ambiente.PublicarAsync(new ExecucaoFinalizadaBuilder().DaOrdem(ordem.OrdemId).ComResumo("Serviço concluído.").Build());
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "Concluida");

        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        os.EstoqueReservado.Should().BeTrue("a peca foi consumida no reparo");
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(EstoqueDepoisDaReserva);
        (await ambiente.SagaAsync(ordem.OrdemId)).PagamentoId.Should().Be(pagamentoId);
        ambiente.Servicos.NoBilling<GerarOrcamento>(ordem.OrdemId).Should().Be(1);
        ambiente.Servicos.NoBilling<SolicitarPagamento>(ordem.OrdemId).Should().Be(1);
        ambiente.Servicos.NaExecucao<SolicitarDiagnostico>(ordem.OrdemId).Should().Be(1);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);
        (await ambiente.OutboxVaziaAsync()).Should().BeTrue();

        // E-mail so depois do commit e uma vez por transicao confirmada: EmDiagnostico, AguardandoAprovacao, AguardandoPagamento,
        // o link, EmExecucao e Finalizada. Nenhuma tentativa desfeita deixa e-mail fantasma.
        ambiente.Infra.Emails.Enviados.Should().HaveCount(6);
        ambiente.Infra.Logs.Registros.Where(r => r.Nivel >= LogLevel.Error).Should().BeEmpty();
    }

    [Fact]
    public async Task Recusa_DeveRecusarAOsCancelarOOrcamentoESemReservarEstoque()
    {
        await using var ambiente = await CriarAsync();
        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");

        await ambiente.EnviarAsync(new RecusarOrcamentoCommand(ordem.OrdemId, "Achei caro."));
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "OrcamentoRecusado");

        (await ambiente.OrdemAsync(ordem.OrdemId)).Status.Should().Be(StatusOrdemDeServico.OrcamentoRecusado);
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial);
        (await Esperar.AteAsync(() => ambiente.Servicos.NoBilling<CancelarOrcamento>(ordem.OrdemId) == 1)).Should().BeTrue();
        ambiente.Servicos.NoBilling<SolicitarPagamento>(ordem.OrdemId).Should().Be(0);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PagamentoNaoPagoNoPrazo_DeveSerCanceladoPeloSweeperELiberarOEstoque()
    {
        await using var ambiente = await CriarAsync();
        var ordem = await ambiente.CriarOrdemAsync();
        await ChegarEmAguardandoPagamentoAsync(ambiente, ordem);
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(EstoqueDepoisDaReserva);

        // O prazo de pagamento (48 h) ainda nao venceu: o sweeper nao publica nada.
        await ambiente.RodarSweeperAsync();
        (await ambiente.EstadoDaSagaAsync(ordem.OrdemId)).Should().Be("AguardandoPagamento");

        ambiente.Relogio.Advance(TimeSpan.FromHours(49));
        await ambiente.RodarSweeperAsync();

        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "Cancelada");
        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.EstoqueReservado.Should().BeFalse();
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial);
        ambiente.Servicos.NoBilling<CancelarPagamento>(ordem.OrdemId).Should().Be(1);
        (await ambiente.Infra.ConsultarAsync(db => Task.FromResult(db.SagaPrazos.Where(p => p.CorrelationId == ordem.OrdemId).All(p => p.Processado || p.Tipo == OficinaMecanica.Contracts.Common.TipoPrazo.Aprovacao))))
            .Should().BeTrue("o prazo de pagamento foi consumido pelo sweeper");
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecucaoInviavel_DeveEstornarCancelarAOsELiberarOEstoque()
    {
        await using var ambiente = await CriarAsync();
        var ordem = await ambiente.CriarOrdemAsync();
        await ChegarEmAguardandoPagamentoAsync(ambiente, ordem);
        var pagamentoId = ComportamentoDosServicos.PagamentoDa(ordem.OrdemId);
        await ambiente.PublicarAsync(new PagamentoAprovadoBuilder().DaOrdem(ordem.OrdemId).ComPagamento(pagamentoId).ComValor(270m).Build());
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "EmExecucao");

        await ambiente.PublicarAsync(new ExecucaoInviavelBuilder().DaOrdem(ordem.OrdemId).ComMotivo("Peça descontinuada.").Build());

        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "Cancelada");
        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.MotivoCancelamento.Should().Be("Execução inviável: Peça descontinuada.");
        os.EstoqueReservado.Should().BeFalse();
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial);
        ambiente.Servicos.NoBilling<EstornarPagamento>(ordem.OrdemId).Should().Be(1);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecucaoInviavelComEstornoQueFalha_DeveFicarEmEstornoPendenteManualEFecharQuandoOStaffEstornar()
    {
        await using var ambiente = await CriarAsync();
        ambiente.Servicos.EstornoFalha = true;
        var ordem = await ambiente.CriarOrdemAsync();
        await ChegarEmAguardandoPagamentoAsync(ambiente, ordem);
        var pagamentoId = ComportamentoDosServicos.PagamentoDa(ordem.OrdemId);
        await ambiente.PublicarAsync(new PagamentoAprovadoBuilder().DaOrdem(ordem.OrdemId).ComPagamento(pagamentoId).ComValor(270m).Build());
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "EmExecucao");

        await ambiente.PublicarAsync(new ExecucaoInviavelBuilder().DaOrdem(ordem.OrdemId).ComMotivo("Peça descontinuada.").Build());
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "EstornoPendenteManual");

        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.EmExecucao, "o dinheiro nao voltou: nao vira Cancelada");
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(EstoqueDepoisDaReserva);
        ambiente.Infra.Logs.Registros.Should().Contain(r => r.Nivel == LogLevel.Error && r.Mensagem.Contains("EstornoPendenteManual"));

        // O staff refaz o estorno na API do Billing: o PagamentoEstornado resultante fecha a saga.
        await ambiente.PublicarAsync(new PagamentoEstornadoBuilder().DaOrdem(ordem.OrdemId).ComPagamento(pagamentoId).Build());
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "Cancelada");
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);
    }

    private const string FalhaDoGateway = "Gateway de pagamento indisponível (falha simulada).";

    private static bool LogDeFaultTecnico(AmbienteDaSagaInfra a, string comando, Guid ordemId, Guid messageIdDoComando, string erro) =>
        a.Infra.Logs.Registros.Any(r =>
            r.Nivel == LogLevel.Error
            && r.Mensagem.Contains($"Falha técnica no comando {comando} da ordem de serviço {ordemId}")
            && r.Mensagem.Contains($"mensagem {messageIdDoComando}")
            && r.Mensagem.Contains(erro));

    private async Task<int> MensagensEmErroDoBillingAsync(AmbienteDaSagaInfra a) =>
        await _infra.MensagensNaFilaAsync(a.Infra.VirtualHost, Filas.Billing + "_error");

    [Fact]
    public async Task FaultRealDeSolicitarPagamento_ConsumerDoBillingLanca_DeveChegarNaSagaPeloBrokerECompensarComEstoqueDevolvido()
    {
        await using var ambiente = await CriarAsync();
        ambiente.Servicos.LancarExcecaoTecnicaEm<SolicitarPagamento>(FalhaDoGateway);
        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");

        await ambiente.AprovarAsync(ordem.OrdemId);
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "Cancelada");

        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.MotivoCancelamento.Should().Be("Falha técnica ao solicitar o pagamento.");
        os.EstoqueReservado.Should().BeFalse();
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial, "a compensacao devolve a reserva");
        (await Esperar.AteAsync(() => ambiente.Servicos.NoBilling<CancelarOrcamento>(ordem.OrdemId) == 1)).Should().BeTrue("a saga cancela o orcamento no Billing");

        var comando = ambiente.Servicos.RecebidoPeloBilling.OfType<SolicitarPagamento>().Should().ContainSingle("o Billing recebeu o comando uma vez e a saga nao o reenviou").Subject;
        LogDeFaultTecnico(ambiente, nameof(SolicitarPagamento), ordem.OrdemId, comando.MessageId, FalhaDoGateway)
            .Should().BeTrue("o Fault deixa o log de erro com os ids da OS e do comando e a causa");
        (await MensagensEmErroDoBillingAsync(ambiente)).Should().Be(1, "o comando que falhou fica na fila _error do destino, pronto para redrive");
        (await _infra.MensagensNaFilaAsync(ambiente.Infra.VirtualHost, Filas.Saga + "_error")).Should().Be(0, "o Fault e tratado pela saga, nao vai para _error");
        (await _infra.MensagensNaFilaAsync(ambiente.Infra.VirtualHost, Filas.Execucao + "_error")).Should().Be(0);
    }

    // Cancelamento do pagamento por prazo vencido cujo consumer do Billing lanca: o estado de partida do runbook de redrive.
    private static async Task<(AmbienteDaSagaInfra.OrdemCriada Ordem, CancelarPagamento Comando)> ChegarEmCancelandoPagamentoComFaultAsync(AmbienteDaSagaInfra ambiente)
    {
        var ordem = await ambiente.CriarOrdemAsync();
        await ChegarEmAguardandoPagamentoAsync(ambiente, ordem);
        ambiente.Servicos.LancarExcecaoTecnicaEm<CancelarPagamento>(FalhaDoGateway);

        ambiente.Relogio.Advance(TimeSpan.FromHours(49));
        await ambiente.RodarSweeperAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "CancelandoPagamento");
        (await Esperar.AteAsync(() => ambiente.Servicos.NoBilling<CancelarPagamento>(ordem.OrdemId) == 1)).Should().BeTrue();

        var comando = ambiente.Servicos.RecebidoPeloBilling.OfType<CancelarPagamento>().Single();
        (await Esperar.AteAsync(() => LogDeFaultTecnico(ambiente, nameof(CancelarPagamento), ordem.OrdemId, comando.MessageId, FalhaDoGateway), TimeSpan.FromSeconds(30)))
            .Should().BeTrue("o Fault real do CancelarPagamento chega na saga e deixa o log de erro");
        return (ordem, comando);
    }

    [Fact]
    public async Task FaultRealDeCancelarPagamento_ConsumerDoBillingLanca_DeveSoLogarEManterASagaEmCancelandoPagamentoSemReenviar()
    {
        await using var ambiente = await CriarAsync();
        var (ordem, _) = await ChegarEmCancelandoPagamentoComFaultAsync(ambiente);

        // Tempo para uma reacao indevida (reenvio automatico, transicao) aparecer: a decisao e so alertar.
        await Task.Delay(TimeSpan.FromSeconds(3));

        (await ambiente.EstadoDaSagaAsync(ordem.OrdemId)).Should().Be("CancelandoPagamento");
        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento, "so o Billing confirma o que aconteceu com o pagamento");
        os.EstoqueReservado.Should().BeTrue("o estoque so volta depois da resposta do Billing");
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(EstoqueDepoisDaReserva);
        ambiente.Servicos.NoBilling<CancelarPagamento>(ordem.OrdemId).Should().Be(1, "a saga nao reenvia o comando por conta propria");
        (await MensagensEmErroDoBillingAsync(ambiente)).Should().Be(1, "o comando espera na _error do Billing");
    }

    [Fact]
    public async Task RedriveDoCancelarPagamento_DepoisQueOBillingVolta_DeveReprocessarComOMesmoIdEFecharASaga()
    {
        await using var ambiente = await CriarAsync();
        var (ordem, comando) = await ChegarEmCancelandoPagamentoComFaultAsync(ambiente);

        // O Billing foi consertado e o operador devolve a mensagem da _error para a fila de comandos.
        ambiente.Servicos.PararDeLancarExcecaoTecnicaEm<CancelarPagamento>();
        var movidas = await _infra.RedriveDaFilaDeErroAsync(ambiente.Infra.VirtualHost, Filas.Billing);
        movidas.Should().Be(1);

        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "Cancelada");
        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.EstoqueReservado.Should().BeFalse();
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial);

        var recebidos = ambiente.Servicos.RecebidoPeloBilling.OfType<CancelarPagamento>().ToList();
        recebidos.Should().HaveCount(2, "a tentativa que falhou e o redrive");
        recebidos.Select(c => c.MessageId).Distinct().Should().ContainSingle().Which.Should().Be(comando.MessageId, "o id deterministico e o que torna o reprocessamento idempotente");
        (await ambiente.MensagensEmErroAsync()).Should().Be(0, "depois do redrive a _error esvazia");
    }

    [Fact]
    public async Task CancelamentoAntesDeCriar_OBillingRegistraOCancelamentoPrevio_EAsagaIgnoraAsRespostasTardias()
    {
        await using var ambiente = await CriarAsync();
        ambiente.Servicos.PortaDoGerarOrcamento = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "GerandoOrcamento");
        (await Esperar.AteAsync(() => ambiente.Servicos.NoBilling<GerarOrcamento>(ordem.OrdemId) == 1)).Should().BeTrue();

        // O staff cancela enquanto o Billing ainda nao terminou de gerar o orcamento.
        await ambiente.EnviarAsync(new CancelarOrdemDeServicoCommand(ordem.OrdemId, "Cliente desistiu."));
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "Cancelada");

        (await Esperar.AteAsync(() => ambiente.Servicos.NoBilling<CancelarOrcamento>(ordem.OrdemId) == 1 && ambiente.Servicos.NaExecucao<CancelarProducao>(ordem.OrdemId) == 1))
            .Should().BeTrue("CancelarOrcamento e CancelarProducao saem junto do cancelamento");
        ambiente.Servicos.RecebidoPeloBilling.OfType<CancelarOrcamento>().Single().OrcamentoId.Should().BeNull("a saga nao conhecia o orcamento");

        // O Billing termina o GerarOrcamento atrasado: ve o cancelamento previo e responde OrcamentoNaoGerado, que a saga ja
        // cancelada ignora (nao vai para _error e nao muda nada).
        ambiente.Servicos.PortaDoGerarOrcamento.SetResult();
        await Task.Delay(TimeSpan.FromSeconds(3));

        (await ambiente.EstadoDaSagaAsync(ordem.OrdemId)).Should().Be("Cancelada");
        (await ambiente.OrdemAsync(ordem.OrdemId)).Status.Should().Be(StatusOrdemDeServico.Cancelada);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);
        ambiente.Infra.Logs.Registros.Where(r => r.Nivel >= LogLevel.Error).Should().BeEmpty();
    }

    [Fact]
    public async Task EventoQueChegaAntesDaSaga_DeveFalharEReprocessarEAplicarDepoisQueAAberturaCriarAInstancia()
    {
        var retry = Enumerable.Range(0, 20).Select(i => ($"Mensageria:RetryIntervalosMs:{i}", (string?)"300")).ToArray();
        await using var ambiente = await AmbienteDaSagaInfra.CriarAsync(_infra, configuracao: retry);
        ambiente.Servicos.DiagnosticoAutomatico = false;
        var ordem = await ambiente.SemearOrdemSemSagaAsync();

        await ambiente.PublicarAsync(new DiagnosticoIniciadoBuilder().DaOrdem(ordem.OrdemId).Build());
        await Task.Delay(500);
        (await ambiente.EstadoDaSagaAsync(ordem.OrdemId)).Should().BeNull("a saga ainda nao existe: o evento falha e espera o retry");

        await ambiente.PublicarAsync(new OrdemDeServicoAbertaBuilder().DaOrdem(ordem.OrdemId).Build());

        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "EmDiagnostico");
        (await ambiente.OrdemAsync(ordem.OrdemId)).Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0, "o retry resolveu: nada vai para _error");
    }
}
