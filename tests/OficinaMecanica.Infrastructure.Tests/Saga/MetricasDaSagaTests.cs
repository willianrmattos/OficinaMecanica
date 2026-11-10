using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// As metricas saem do observador da maquina (transicoes), das activities que so avisam (falha de compensacao, decisao ignorada)
// e dos gauges. Cada teste roda a maquina REAL no harness e le o que um MeterListener recebeu. O Meter e estatico, entao a
// colecao desliga o paralelismo: nenhuma outra saga emite enquanto estes testes rodam.
[Collection(ColecaoDeMetricas.Nome)]
public class MetricasDaSagaTests
{
    private static double Somar(CapturadorDeMetricas metricas, string instrumento, params (string Chave, string Valor)[] tags) =>
        metricas.Medicoes(instrumento).Where(m => tags.All(t => m.Tag(t.Chave) == t.Valor)).Sum(m => m.Valor);

    // ---- Transicoes ----

    [Fact]
    public async Task CaminhoFeliz_DeveContarIniciadaEConcluidaEMedirOTempoEmCadaEstadoQueASagaDeixou()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        var relogio = cenario.Ambiente.Relogio;

        await cenario.AbrirAsync();
        Somar(metricas, "saga_iniciada").Should().Be(1);
        metricas.Medicoes("saga_duracao_estado_segundos").Should().BeEmpty("a abertura nao deixa estado nenhum");

        relogio.Advance(TimeSpan.FromMinutes(2));
        await cenario.IniciarDiagnosticoAsync();
        relogio.Advance(TimeSpan.FromHours(1));
        await cenario.ConcluirDiagnosticoAsync();
        relogio.Advance(TimeSpan.FromSeconds(10));
        await cenario.GerarOrcamentoAsync();
        relogio.Advance(TimeSpan.FromHours(6));
        await cenario.AprovarAsync();
        relogio.Advance(TimeSpan.FromMinutes(30));
        await cenario.SolicitarPagamentoAsync();
        relogio.Advance(TimeSpan.FromMinutes(1));
        await cenario.PagarAsync();
        relogio.Advance(TimeSpan.FromDays(2));
        await cenario.PublicarAsync(new ExecucaoFinalizadaBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.AguardarEstadoAsync(m => m.Concluida);

        Somar(metricas, "saga_iniciada").Should().Be(1);
        Somar(metricas, "saga_concluida").Should().Be(1);
        Somar(metricas, "saga_cancelada").Should().Be(0);
        Somar(metricas, "saga_orcamento_recusado").Should().Be(0);
        Somar(metricas, "saga_estorno_pendente_manual").Should().Be(0);

        Somar(metricas, "saga_duracao_estado_segundos", ("estado", "AguardandoDiagnostico")).Should().Be(120);
        Somar(metricas, "saga_duracao_estado_segundos", ("estado", "EmDiagnostico")).Should().Be(3600);
        Somar(metricas, "saga_duracao_estado_segundos", ("estado", "GerandoOrcamento")).Should().Be(10);
        Somar(metricas, "saga_duracao_estado_segundos", ("estado", "AguardandoAprovacao")).Should().Be(6 * 3600);

        // Medida desde a ultima atualizacao da saga: o PagamentoSolicitado (que atualiza e nao muda de estado) chegou 30 minutos
        // depois da entrada em AguardandoPagamento, entao a duracao conta a partir dele.
        Somar(metricas, "saga_duracao_estado_segundos", ("estado", "AguardandoPagamento")).Should().Be(60);
        Somar(metricas, "saga_duracao_estado_segundos", ("estado", "EmExecucao")).Should().Be(2 * 24 * 3600);
        metricas.Medicoes("saga_duracao_estado_segundos").Should().HaveCount(6);
    }

    [Fact]
    public async Task OrcamentoRecusadoPeloCliente_DeveContarARecusaComACausaENaoContarPrazo()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();

        await cenario.ChegarEmOrcamentoRecusadoAsync();

        Somar(metricas, "saga_orcamento_recusado", ("causa", "ClienteRecusouOrcamento")).Should().Be(1);
        Somar(metricas, "saga_orcamento_recusado").Should().Be(1);
        metricas.Medicoes("saga_prazo_expirado").Should().BeEmpty();
        Somar(metricas, "saga_cancelada").Should().Be(0);
    }

    [Fact]
    public async Task PrazoDeAprovacaoVigente_DeveContarOPrazoEARecusaPorPrazo()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        await cenario.GerarOrcamentoAsync();

        await cenario.PublicarAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Aprovacao).ComPrazo(cenario.Saga().PrazoAprovacaoId!.Value).Build());
        await cenario.AguardarEstadoAsync(m => m.OrcamentoRecusado);

        Somar(metricas, "saga_prazo_expirado", ("tipo", "Aprovacao")).Should().Be(1);
        Somar(metricas, "saga_orcamento_recusado", ("causa", "PrazoExpirado")).Should().Be(1);
    }

    [Fact]
    public async Task PrazoDePagamentoVigente_DeveContarOPrazoECancelarDepoisDoPagamentoCancelado()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();

        await cenario.ChegarEmCancelandoPagamentoAsync();
        Somar(metricas, "saga_prazo_expirado", ("tipo", "Pagamento")).Should().Be(1);
        Somar(metricas, "saga_cancelada").Should().Be(0, "a ordem so e cancelada quando o Billing responde");

        await cenario.PublicarAsync(new PagamentoCanceladoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        Somar(metricas, "saga_cancelada", ("estado_origem", "CancelandoPagamento")).Should().Be(1);
        Somar(metricas, "saga_prazo_expirado").Should().Be(1);
    }

    [Fact]
    public async Task CancelamentoDoStaffEmDiagnostico_DeveContarCanceladaComOEstadoDeOrigem()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();

        await cenario.CancelarPeloStaffAsync();
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        Somar(metricas, "saga_cancelada", ("estado_origem", "EmDiagnostico")).Should().Be(1);
    }

    [Fact]
    public async Task EstornoFalhou_DeveContarEstornoPendenteManualComACausa()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();

        await cenario.ChegarEmEstornoPendenteManualAsync();

        Somar(metricas, "saga_estorno_pendente_manual", ("causa", "EstornoFalhou")).Should().Be(1);
        Somar(metricas, "saga_cancelada").Should().Be(0);
    }

    [Fact]
    public async Task FalhaTecnicaAoEstornar_DeveContarEstornoPendenteManualComACausaDoFault()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmEstornandoAsync();

        await cenario.PublicarFalhaAsync<EstornarPagamento>("Gateway fora.");
        await cenario.AguardarEstadoAsync(m => m.EstornoPendenteManual);

        Somar(metricas, "saga_estorno_pendente_manual", ("causa", "Fault<EstornarPagamento>")).Should().Be(1);
        Somar(metricas, "saga_compensacao_falhou").Should().Be(0, "EstornarPagamento nao e um dos tres comandos de compensacao");
    }

    // ---- Eventos que nao mudam nada ----

    [Fact]
    public async Task EventoIgnoradoEPrazoQueNaoEOVigente_NaoDevemEmitirNada()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        var antes = metricas.Todas.Count;

        await cenario.PublicarIgnoradoAsync(new DiagnosticoIniciadoBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.PublicarIgnoradoAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Aprovacao).ComPrazo(Guid.NewGuid()).Build());
        await cenario.PublicarIgnoradoAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Pagamento).ComPrazo(Guid.NewGuid()).Build());

        metricas.Todas.Count.Should().Be(antes, "evento sem transicao nao e transicao: nem contador, nem duracao, nem prazo");
        Somar(metricas, "saga_prazo_expirado").Should().Be(0, "so o prazo que decidiu algo conta");
    }

    [Fact]
    public async Task TransicaoQueFalhaNoMeio_NaoDeveContarNada()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        var falhas = new FalhasInjetadas { FalharDepoisDoHandlerDeAprovacao = true };
        await using var cenario = await CenarioDaSaga.CriarAsync(ajustarServicos: servicos =>
        {
            servicos.AddSingleton(falhas);
            servicos.AddTransient<IPipelineBehavior<AplicarAprovacaoDoOrcamentoCommand, Unit>, FalhaDepoisDoHandlerDeAprovacaoBehavior>();
        });
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        await cenario.GerarOrcamentoAsync();
        var antes = metricas.Todas.Count;
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromHours(1));

        // A activity lanca em todas as tentativas: a mensagem esgota o retry e o MassTransit publica o Fault dela.
        var aprovacao = new ClienteAprovouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build();
        await cenario.Ambiente.Harness.Bus.Publish(aprovacao);
        (await cenario.Ambiente.Harness.Published.Any<Fault<ClienteAprovouOrcamento>>()).Should().BeTrue("o retry da aprovacao deveria esgotar");

        cenario.Saga().CurrentState.Should().Be(cenario.Maquina.AguardandoAprovacao.Name);
        metricas.Todas.Count.Should().Be(antes, "uma transicao desfeita nao deixa metrica; a que o retry refizer e que conta");
    }

    // ---- Avisos sem transicao ----

    [Theory]
    [InlineData("ClienteAprovouOrcamento")]
    [InlineData("ClienteRecusouOrcamento")]
    public async Task DecisaoDoClienteEmEstadoQueNaoAceita_DeveContarADecisaoIgnoradaComEstado(string decisao)
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmExecucaoAsync();

        if (decisao == "ClienteAprovouOrcamento")
            await cenario.PublicarIgnoradoAsync(new ClienteAprovouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());
        else
            await cenario.PublicarIgnoradoAsync(new ClienteRecusouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());

        Somar(metricas, "saga_decisao_ignorada", ("decisao", decisao), ("estado", "EmExecucao")).Should().Be(1);
        Somar(metricas, "saga_decisao_ignorada").Should().Be(1);
    }

    [Fact]
    public async Task CancelamentoDoStaffComOReparoEmAndamento_DeveContarADecisaoIgnorada()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmExecucaoAsync();

        await cenario.PublicarIgnoradoAsync(new CancelamentoSolicitadoBuilder().DaOrdem(cenario.OrdemId).Build());

        Somar(metricas, "saga_decisao_ignorada", ("decisao", "CancelamentoSolicitado"), ("estado", "EmExecucao")).Should().Be(1);
    }

    [Fact]
    public async Task DecisaoQueAAsagaAceita_NaoDeveContarDecisaoIgnorada()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();

        await cenario.ChegarEmAguardandoPagamentoAsync();

        metricas.Medicoes("saga_decisao_ignorada").Should().BeEmpty();
    }

    [Fact]
    public async Task FaultDeComandoDeCompensacao_DeveContarUmPorFaultComATagDoComando()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmCancelandoPagamentoAsync();

        await cenario.PublicarFalhaAsync<CancelarPagamento>("Billing fora.");

        Somar(metricas, "saga_compensacao_falhou", ("comando", "CancelarPagamento")).Should().Be(1);
        Somar(metricas, "saga_compensacao_falhou").Should().Be(1);
        cenario.Saga().CurrentState.Should().Be(cenario.Maquina.CancelandoPagamento.Name, "a compensacao que falha nao muda o estado");
    }

    [Fact]
    public async Task FaultDeCancelarOrcamento_DeveContarComATagDoComando()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmOrcamentoRecusadoAsync();

        await cenario.PublicarFalhaAsync<CancelarOrcamento>("Billing fora.");

        Somar(metricas, "saga_compensacao_falhou", ("comando", "CancelarOrcamento")).Should().Be(1);
    }

    [Fact]
    public async Task FaultDeCancelarProducao_DeveContarComATagDoComando()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.CancelarPeloStaffAsync();
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        await cenario.PublicarFalhaAsync<CancelarProducao>("Execucao fora.");

        Somar(metricas, "saga_compensacao_falhou", ("comando", "CancelarProducao")).Should().Be(1);
    }

    [Fact]
    public async Task FaultDeComandoQueNaoECompensacao_NaoDeveContarCompensacaoFalhou()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();

        await cenario.PublicarFalhaAsync<SolicitarPagamento>("Gateway fora.");
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        metricas.Medicoes("saga_compensacao_falhou").Should().BeEmpty();
    }

    // ---- Tags: so cardinalidade baixa, nunca id, numero ou texto livre ----

    [Fact]
    public async Task Tags_NuncaDevemCarregarIdDaOsNumeroNemTextoLivre()
    {
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmEstornoPendenteManualAsync();
        await cenario.PublicarIgnoradoAsync(new ClienteAprovouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());

        var numero = (await cenario.OrdemAsync()).Numero;
        var valores = metricas.Todas.SelectMany(m => m.Tags.Values).ToList();

        valores.Should().NotBeEmpty();
        valores.Should().NotContain(v => v != null && (v.Contains(cenario.OrdemId.ToString()) || v.Contains(numero)));
        metricas.Todas.SelectMany(m => m.Tags.Keys).Distinct().Should()
            .BeSubsetOf(new[] { "estado", "estado_origem", "causa", "comando", "decisao", "tipo" });
    }
}
