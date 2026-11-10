using FluentAssertions;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// CancelamentoSolicitado (staff) em cada estado, e o pagamento tardio que decide, pelo motivo do cancelamento, se a OS
// segue como paga ou se o dinheiro volta.
public class SagaCancelamentoDoStaffTests
{
    private const string Motivo = "Cliente desistiu do serviço.";

    [Fact]
    public async Task AguardandoDiagnostico_CancelamentoSolicitado_DeveCancelarAProducaoEAOsSemTocarNoBilling()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();

        await cenario.CancelarPeloStaffAsync(Motivo);
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be(Motivo);
        cenario.EnviadosDoTipo<CancelarProducao>().Should().ContainSingle().Which.Motivo.Should().Be(Motivo);
        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().BeEmpty();
        cenario.EnviadosDoTipo<CancelarPagamento>().Should().BeEmpty();
        cenario.Saga().CancelamentoPorStaff.Should().BeFalse("a flag so importa quando ha pagamento a decidir");
    }

    [Fact]
    public async Task EmDiagnostico_CancelamentoSolicitado_DeveCancelarAProducaoEAOs()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();

        await cenario.CancelarPeloStaffAsync(Motivo);
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.Cancelada);
        cenario.EnviadosDoTipo<CancelarProducao>().Should().ContainSingle();
        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().BeEmpty();
    }

    [Fact]
    public async Task GerandoOrcamento_CancelamentoSolicitado_DeveCancelarProducaoEOrcamentoSemOrcamentoIdEAOs()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();

        await cenario.CancelarPeloStaffAsync(Motivo);
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.Cancelada);
        cenario.EnviadosDoTipo<CancelarProducao>().Should().ContainSingle();
        var cancelamento = cenario.EnviadosDoTipo<CancelarOrcamento>().Should().ContainSingle().Subject;
        cancelamento.OrcamentoId.Should().BeNull("o orcamento ainda nao foi confirmado: o Billing localiza pela OS ou registra o cancelamento previo");
        cancelamento.Motivo.Should().Be(Motivo);
    }

    [Fact]
    public async Task AguardandoAprovacao_CancelamentoSolicitado_DeveCancelarProducaoEOrcamentoEAOs()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        await cenario.GerarOrcamentoAsync();

        await cenario.CancelarPeloStaffAsync(Motivo);
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.Cancelada);
        cenario.EnviadosDoTipo<CancelarProducao>().Should().ContainSingle();
        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().ContainSingle().Which.OrcamentoId.Should().Be(cenario.OrcamentoId);
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial, "nada foi reservado ainda");
    }

    [Fact]
    public async Task AguardandoPagamento_CancelamentoSolicitado_DeveMarcarOStaffCancelarPagamentoEProducaoEAguardarOBilling()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        await cenario.SolicitarPagamentoAsync();

        await cenario.CancelarPeloStaffAsync(Motivo);
        await cenario.AguardarEstadoAsync(m => m.CancelandoPagamento);

        cenario.Saga().CancelamentoPorStaff.Should().BeTrue();
        cenario.EnviadosDoTipo<CancelarPagamento>().Should().ContainSingle().Which.PagamentoId.Should().Be(cenario.PagamentoId);
        cenario.EnviadosDoTipo<CancelarProducao>().Should().ContainSingle().Which.Motivo.Should().Be(Motivo);
        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento, "a OS so cancela quando o Billing confirmar");
        ordem.EstoqueReservado.Should().BeTrue();
    }

    [Fact]
    public async Task CancelandoPagamento_CancelamentoSolicitadoDepoisDoPrazo_DeveRegistrarOStaffEDesfazerAProducaoSemMudarDeEstado()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmCancelandoPagamentoAsync();

        await cenario.CancelarPeloStaffAsync(Motivo);

        cenario.Saga().CurrentState.Should().Be(cenario.Maquina.CancelandoPagamento.Name);
        cenario.Saga().CancelamentoPorStaff.Should().BeTrue();
        cenario.Saga().MotivoCancelamento.Should().Be(Motivo);
        cenario.EnviadosDoTipo<CancelarProducao>().Should().ContainSingle();
        cenario.EnviadosDoTipo<CancelarPagamento>().Should().ContainSingle("o CancelarPagamento do prazo ja esta a caminho");
    }

    [Fact]
    public async Task EmExecucao_CancelamentoSolicitado_DeveSerIgnoradoPorqueReparoEmAndamentoNaoSeCancela()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmExecucaoAsync();

        await cenario.PublicarIgnoradoAsync(new CancelamentoSolicitadoBuilder().DaOrdem(cenario.OrdemId).Build());

        cenario.EnviadosDoTipo<CancelarProducao>().Should().BeEmpty();
    }

    [Fact]
    public async Task EstadosDeCompensacao_CancelamentoSolicitado_DeveSerIgnorado()
    {
        await using var estornando = await CenarioDaSaga.CriarAsync();
        await estornando.ChegarEmEstornandoAsync();
        await estornando.PublicarIgnoradoAsync(new CancelamentoSolicitadoBuilder().DaOrdem(estornando.OrdemId).Build());

        await using var pendente = await CenarioDaSaga.CriarAsync();
        await pendente.ChegarEmEstornoPendenteManualAsync();
        await pendente.PublicarIgnoradoAsync(new CancelamentoSolicitadoBuilder().DaOrdem(pendente.OrdemId).Build());
    }

    [Fact]
    public async Task EstadosFinais_CancelamentoSolicitado_DeveSerIgnorado()
    {
        await using var concluida = await CenarioDaSaga.CriarAsync();
        await concluida.ChegarEmConcluidaAsync();
        await concluida.PublicarIgnoradoAsync(new CancelamentoSolicitadoBuilder().DaOrdem(concluida.OrdemId).Build());

        await using var cancelada = await CenarioDaSaga.CriarAsync();
        await cancelada.ChegarEmCanceladaAsync();
        await cancelada.PublicarIgnoradoAsync(new CancelamentoSolicitadoBuilder().DaOrdem(cancelada.OrdemId).Build());

        await using var recusada = await CenarioDaSaga.CriarAsync();
        await recusada.ChegarEmOrcamentoRecusadoAsync();
        await recusada.PublicarIgnoradoAsync(new CancelamentoSolicitadoBuilder().DaOrdem(recusada.OrdemId).Build());
    }

    [Fact]
    public async Task CancelandoPagamento_PagamentoTardioDeCancelamentoPorPrazo_DeveSeguirComoPagoEIniciarAExecucao()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmCancelandoPagamentoAsync();

        await cenario.PublicarAsync(new PagamentoAprovadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.AguardarEstadoAsync(m => m.EmExecucao);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        ordem.EstoqueReservado.Should().BeTrue("a OS foi paga e segue");
        cenario.EnviadosDoTipo<IniciarExecucao>().Should().ContainSingle();
        cenario.EnviadosDoTipo<EstornarPagamento>().Should().BeEmpty();
        cenario.Saga().MotivoCancelamento.Should().BeNull("o motivo do cancelamento por prazo nao vale mais");
        cenario.Saga().PrazoPagamentoId.Should().BeNull();
    }

    [Fact]
    public async Task CancelandoPagamento_PagamentoTardioDeCancelamentoDoStaff_DeveEstornarEmVezDeExecutar()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        await cenario.SolicitarPagamentoAsync();
        await cenario.CancelarPeloStaffAsync(Motivo);
        await cenario.AguardarEstadoAsync(m => m.CancelandoPagamento);

        await cenario.PublicarAsync(new PagamentoAprovadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.AguardarEstadoAsync(m => m.Estornando);

        var estorno = cenario.EnviadosDoTipo<EstornarPagamento>().Should().ContainSingle().Subject;
        estorno.PagamentoId.Should().Be(cenario.PagamentoId);
        estorno.Motivo.Should().Be(Motivo);
        cenario.EnviadosDoTipo<IniciarExecucao>().Should().BeEmpty("a producao ja foi desfeita");
        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
    }

    [Fact]
    public async Task PagamentoTardioDoStaff_DepoisDoEstorno_DeveCancelarAOsELiberarOEstoque()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        await cenario.CancelarPeloStaffAsync(Motivo);
        await cenario.AguardarEstadoAsync(m => m.CancelandoPagamento);
        await cenario.PublicarAsync(new PagamentoAprovadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.AguardarEstadoAsync(m => m.Estornando);

        await cenario.PublicarAsync(new PagamentoEstornadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be(Motivo);
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
    }
}
