using FluentAssertions;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// Fault<T> e falha TECNICA do destino (o retry dele esgotou): a saga registra o sinal no log e aplica a compensacao
// conservadora de cada comando, documentada no ADR 0008. Falha de negocio chega como evento explicito, nao como Fault. Aqui o
// Fault e montado a mao e publicado no bus em memoria (cobre cada comando e estado); o caminho real, com um consumer que lanca
// e o broker publicando o Fault na fila da saga, esta em SagaPonteAPonteTests.
public class SagaFalhasTecnicasTests
{
    // O log de Fault e o sinal de alerta (ADR 0008): template estavel com o comando, a OS, o estado da saga e o MessageId do
    // comando, que e o id da mensagem que espera na fila _error do destino.
    private static void DeveTerLogadoAFalha<TComando>(CenarioDaSaga cenario, string erro) where TComando : class
    {
        var messageIdDoComando = MessageIdDoContrato<TComando>.Obter(cenario.EnviadosDoTipo<TComando>().Last());

        cenario.Ambiente.Logs.Registros.Should().Contain(r =>
            r.Nivel == LogLevel.Error
            && r.Mensagem.StartsWith($"Falha técnica no comando {typeof(TComando).Name} da ordem de serviço {cenario.OrdemId} (saga em ")
            && r.Mensagem.Contains($"), mensagem {messageIdDoComando} no host ")
            && r.Mensagem.Contains(erro));
    }

    [Fact]
    public async Task AguardandoDiagnostico_FaultDeSolicitarDiagnostico_DeveCancelarAOsELogarAFalha()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();

        await cenario.PublicarFalhaAsync<SolicitarDiagnostico>("Mongo indisponível.");
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be("Falha técnica ao solicitar o diagnóstico.");
        DeveTerLogadoAFalha<SolicitarDiagnostico>(cenario, "Mongo indisponível.");
    }

    [Fact]
    public async Task GerandoOrcamento_FaultDeGerarOrcamento_DeveCancelarAOsELogarAFalha()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();

        await cenario.PublicarFalhaAsync<GerarOrcamento>("SQL do Billing fora.");
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be("Falha técnica ao gerar o orçamento.");
        DeveTerLogadoAFalha<GerarOrcamento>(cenario, "SQL do Billing fora.");
    }

    [Fact]
    public async Task AguardandoPagamento_FaultDeSolicitarPagamento_DeveSeComportarComoPagamentoNaoSolicitado()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();

        await cenario.PublicarFalhaAsync<SolicitarPagamento>("Gateway fora.");
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be("Falha técnica ao solicitar o pagamento.");
        ordem.EstoqueReservado.Should().BeFalse();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().ContainSingle().Which.OrcamentoId.Should().Be(cenario.OrcamentoId);
        DeveTerLogadoAFalha<SolicitarPagamento>(cenario, "Gateway fora.");
    }

    [Fact]
    public async Task CancelandoPagamento_FaultDeSolicitarPagamento_DeveLiberarOEstoqueECancelarAOs()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmCancelandoPagamentoAsync(comPagamentoSolicitado: false);

        await cenario.PublicarFalhaAsync<SolicitarPagamento>("Gateway fora.");
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.Cancelada);
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().BeEmpty();
    }

    [Fact]
    public async Task EmExecucao_FaultDeIniciarExecucao_DeveSeComportarComoExecucaoInviavelEEstornar()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmExecucaoAsync();

        await cenario.PublicarFalhaAsync<IniciarExecucao>("Mongo indisponível.");
        await cenario.AguardarEstadoAsync(m => m.Estornando);

        var estorno = cenario.EnviadosDoTipo<EstornarPagamento>().Should().ContainSingle().Subject;
        estorno.PagamentoId.Should().Be(cenario.PagamentoId);
        estorno.Motivo.Should().Be("Falha técnica ao iniciar a execução do serviço.");
        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.EmExecucao, "o dinheiro ainda nao voltou");
        DeveTerLogadoAFalha<IniciarExecucao>(cenario, "Mongo indisponível.");
    }

    [Fact]
    public async Task Estornando_FaultDeEstornarPagamento_DeveIrParaEstornoPendenteManualEAlertar()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmEstornandoAsync();

        await cenario.PublicarFalhaAsync<EstornarPagamento>("Billing fora.");
        await cenario.AguardarEstadoAsync(m => m.EstornoPendenteManual);

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        DeveTerLogadoAFalha<EstornarPagamento>(cenario, "Billing fora.");
        cenario.Ambiente.Logs.Registros.Should().Contain(r => r.Nivel == LogLevel.Error && r.Mensagem.Contains("EstornoPendenteManual") && r.Mensagem.Contains("Billing fora."));
    }

    [Fact]
    public async Task CancelandoPagamento_FaultDeCancelarPagamento_DeveSoLogarEPermanecerEsperandoOBilling()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmCancelandoPagamentoAsync();
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromMinutes(1));
        var antes = await cenario.RetratoAsync();

        await cenario.PublicarFalhaAsync<CancelarPagamento>("Billing fora.");

        (await cenario.RetratoAsync()).Should().Be(antes, "falha de compensacao nao muda estado nem decide nada: so alerta");
        DeveTerLogadoAFalha<CancelarPagamento>(cenario, "Billing fora.");
    }

    [Fact]
    public async Task Cancelada_FaultDeCancelarOrcamento_DeveSoLogarSemMudarNada()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        await cenario.GerarOrcamentoAsync();
        await cenario.CancelarPeloStaffAsync();
        await cenario.AguardarEstadoAsync(m => m.Cancelada);
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromMinutes(1));
        var antes = await cenario.RetratoAsync();

        await cenario.PublicarFalhaAsync<CancelarOrcamento>("Billing fora.");

        (await cenario.RetratoAsync()).Should().Be(antes);
        DeveTerLogadoAFalha<CancelarOrcamento>(cenario, "Billing fora.");
    }

    [Fact]
    public async Task Cancelada_FaultDeCancelarProducaoPorReparoEmAndamento_DeveSoLogarSemMudarNada()
    {
        // A Execucao recusa (DomainException) cancelar um reparo ja iniciado; o Fault chega depois de a saga ter
        // cancelado por outro caminho e nao pode desfazer nem decidir nada.
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.CancelarPeloStaffAsync();
        await cenario.AguardarEstadoAsync(m => m.Cancelada);
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromMinutes(1));
        var antes = await cenario.RetratoAsync();

        await cenario.PublicarFalhaAsync<CancelarProducao>("Reparo em andamento não pode ser cancelado.");

        (await cenario.RetratoAsync()).Should().Be(antes);
        DeveTerLogadoAFalha<CancelarProducao>(cenario, "Reparo em andamento não pode ser cancelado.");
    }

    [Fact]
    public async Task Estornando_FaultDeCancelarProducao_DeveContinuarEmEstornando()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        await cenario.SolicitarPagamentoAsync();
        await cenario.CancelarPeloStaffAsync();
        await cenario.AguardarEstadoAsync(m => m.CancelandoPagamento);
        await cenario.PublicarAsync(new PagamentoAprovadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.AguardarEstadoAsync(m => m.Estornando);
        var antes = await cenario.RetratoAsync();

        await cenario.PublicarFalhaAsync<CancelarProducao>("Reparo em andamento não pode ser cancelado.");

        (await cenario.RetratoAsync()).Should().Be(antes);
    }

    [Fact]
    public async Task FaultsEmEstadoSemCompensacao_DevemSerLogadosEIgnorados()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmExecucaoAsync();
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromMinutes(1));
        var antes = await cenario.RetratoAsync();

        // O comando de diagnostico ja foi atendido: um Fault dele agora (tardio ou duplicado) nao cancela uma OS em execucao.
        await cenario.PublicarFalhaAsync<SolicitarDiagnostico>("Atrasado.");
        await cenario.PublicarFalhaAsync<GerarOrcamento>("Atrasado.");
        await cenario.PublicarFalhaAsync<SolicitarPagamento>("Atrasado.");

        (await cenario.RetratoAsync()).Should().Be(antes);
        DeveTerLogadoAFalha<SolicitarDiagnostico>(cenario, "Atrasado.");
    }
}
