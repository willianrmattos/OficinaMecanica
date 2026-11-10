using FluentAssertions;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// Um teste por compensacao do puml: cada desfecho que nao e o caminho feliz, com o que a saga desfaz (estoque, orcamento,
// pagamento, OS) e o que ela NAO desfaz.
public class SagaCompensacoesTests
{
    private const int EstoqueDepoisDaReserva = CenarioDaSaga.EstoqueInicial - CenarioDaSaga.QuantidadeDaPeca;

    [Fact]
    public async Task GerandoOrcamento_OrcamentoNaoGerado_DeveCancelarAOsComOMotivoDoBillingSemEnviarMaisNada()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();

        await cenario.PublicarAsync(new OrcamentoNaoGeradoBuilder().DaOrdem(cenario.OrdemId).ComMotivo("Valor acima do limite do orçamento.").Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be("Valor acima do limite do orçamento.");
        cenario.TodosOsComandosEnviados().Select(c => c.GetType().Name).Should().Equal(nameof(SolicitarDiagnostico), nameof(GerarOrcamento));
        (await cenario.PrazosAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AguardandoAprovacao_ClienteAprovouSemEstoque_DeveCancelarOOrcamentoEAOsSemCobrarNemReservar()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        await cenario.GerarOrcamentoAsync();
        await cenario.ReduzirEstoqueParaAsync(CenarioDaSaga.QuantidadeDaPeca - 1);

        await cenario.PublicarAsync(new ClienteAprovouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Contain("Estoque insuficiente");
        ordem.EstoqueReservado.Should().BeFalse();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.QuantidadeDaPeca - 1, "a reserva e tudo ou nada");

        var cancelamento = cenario.EnviadosDoTipo<CancelarOrcamento>().Should().ContainSingle().Subject;
        cancelamento.OrcamentoId.Should().Be(cenario.OrcamentoId);
        cancelamento.Motivo.Should().Contain("Estoque insuficiente");
        cenario.EnviadosDoTipo<SolicitarPagamento>().Should().BeEmpty("ninguem e cobrado sem estoque");
        (await cenario.PrazosAsync()).Should().ContainSingle(p => p.Tipo == TipoPrazo.Aprovacao, "nao nasce prazo de pagamento");
    }

    [Theory]
    [InlineData("Achei o valor alto.", "Orçamento recusado pelo cliente. Motivo: Achei o valor alto.", "Achei o valor alto.")]
    [InlineData(null, "Orçamento recusado pelo cliente.", "Orçamento recusado pelo cliente.")]
    [InlineData("   ", "Orçamento recusado pelo cliente.", "Orçamento recusado pelo cliente.")]
    public async Task AguardandoAprovacao_ClienteRecusou_DeveRecusarAOsECancelarOOrcamentoSemMexerNoEstoque(string? motivo, string historicoEsperado, string motivoNoBilling)
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        await cenario.GerarOrcamentoAsync();

        await cenario.PublicarAsync(new ClienteRecusouOrcamentoBuilder().DaOrdem(cenario.OrdemId).ComMotivo(motivo).Build());
        await cenario.AguardarEstadoAsync(m => m.OrcamentoRecusado);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.OrcamentoRecusado);
        ordem.HistoricoStatus.Last().Observacao.Should().Be(historicoEsperado);
        ordem.EstoqueReservado.Should().BeFalse();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
        var cancelamento = cenario.EnviadosDoTipo<CancelarOrcamento>().Should().ContainSingle().Subject;
        cancelamento.OrcamentoId.Should().Be(cenario.OrcamentoId);
        cancelamento.Motivo.Should().Be(motivoNoBilling, "o extrato do Billing diz que foi o cliente, nunca o texto generico do fluxo automatico");
        cenario.Saga().PrazoAprovacaoId.Should().BeNull();
    }

    [Fact]
    public async Task AguardandoAprovacao_PrazoDeAprovacaoVigenteExpirou_DeveRecusarAOsECancelarOOrcamento()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        await cenario.GerarOrcamentoAsync();
        var prazoId = cenario.Saga().PrazoAprovacaoId!.Value;

        await cenario.PublicarAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Aprovacao).ComPrazo(prazoId).Build());
        await cenario.AguardarEstadoAsync(m => m.OrcamentoRecusado);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.OrcamentoRecusado);
        ordem.HistoricoStatus.Last().Observacao.Should().Contain("Prazo para aprovação do orçamento expirado");
        var cancelamento = cenario.EnviadosDoTipo<CancelarOrcamento>().Should().ContainSingle().Subject;
        cancelamento.OrcamentoId.Should().Be(cenario.OrcamentoId);
        cancelamento.Motivo.Should().Be("Prazo para aprovação do orçamento expirado.", "o Billing recebe a causa real do cancelamento");
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
        cenario.Saga().PrazoAprovacaoId.Should().BeNull();
    }

    [Fact]
    public async Task AguardandoPagamento_PrazoDePagamentoVigenteExpirou_DeveCancelarOPagamentoNoBillingEEsperarARespostaSemDecidir()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        await cenario.SolicitarPagamentoAsync();

        await cenario.ExpirarPagamentoAsync();

        var comando = cenario.EnviadosDoTipo<CancelarPagamento>().Should().ContainSingle().Subject;
        comando.PagamentoId.Should().Be(cenario.PagamentoId);
        comando.Motivo.Should().Contain("Pagamento não realizado dentro do prazo");
        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento, "so o Billing confirma se o pagamento caiu");
        ordem.EstoqueReservado.Should().BeTrue("o estoque so volta depois da confirmacao do Billing");
        cenario.Saga().CancelamentoPorStaff.Should().BeFalse();
    }

    [Fact]
    public async Task AguardandoPagamento_PrazoExpirouAntesDoPagamentoSolicitado_DeveCancelarComPagamentoIdNulo()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();

        await cenario.ExpirarPagamentoAsync();

        cenario.EnviadosDoTipo<CancelarPagamento>().Should().ContainSingle().Which.PagamentoId.Should().BeNull();
    }

    [Fact]
    public async Task AguardandoPagamento_PagamentoNaoSolicitado_DeveLiberarOEstoqueCancelarOOrcamentoEAOs()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(EstoqueDepoisDaReserva);

        await cenario.PublicarAsync(new PagamentoNaoSolicitadoBuilder().DaOrdem(cenario.OrdemId).ComMotivo("O gateway recusou a cobrança.").Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be("O gateway recusou a cobrança.");
        ordem.EstoqueReservado.Should().BeFalse();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().ContainSingle().Which.OrcamentoId.Should().Be(cenario.OrcamentoId);
        cenario.EnviadosDoTipo<CancelarPagamento>().Should().BeEmpty("nao ha pagamento a cancelar");
    }

    [Fact]
    public async Task AguardandoPagamento_PagamentoNaoSolicitadoSemMotivo_DeveCancelarOOrcamentoComOMotivoPadraoDoFluxoAutomatico()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();

        await cenario.PublicarAsync(new PagamentoNaoSolicitadoBuilder().DaOrdem(cenario.OrdemId).ComMotivo("  ").Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().ContainSingle()
            .Which.Motivo.Should().Be("Cancelamento realizado pelo fluxo automático da ordem de serviço.", "o cancelamento foi do fluxo automatico: nao ha decisao de pessoa a citar");
    }

    [Fact]
    public async Task CancelandoPagamento_PagamentoCancelado_DeveLiberarOEstoqueECancelarAOsSemCancelarOOrcamentoDeNovo()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmCancelandoPagamentoAsync();

        await cenario.PublicarAsync(new PagamentoCanceladoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Contain("Pagamento não realizado dentro do prazo");
        ordem.EstoqueReservado.Should().BeFalse();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().BeEmpty("o CancelarPagamento do Billing ja cancela o orcamento aprovado");
    }

    [Fact]
    public async Task CancelandoPagamento_PagamentoCanceladoSemPagamentoId_DeveCancelarAOsMesmoAssim()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmCancelandoPagamentoAsync(comPagamentoSolicitado: false);

        await cenario.PublicarAsync(new PagamentoCanceladoBuilder().DaOrdem(cenario.OrdemId).SemPagamento().Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.Cancelada);
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
    }

    [Fact]
    public async Task CancelandoPagamento_PagamentoNaoSolicitado_DeveLiberarOEstoqueECancelarAOs()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmCancelandoPagamentoAsync(comPagamentoSolicitado: false);

        await cenario.PublicarAsync(new PagamentoNaoSolicitadoBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.Cancelada);
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().BeEmpty();
    }

    [Fact]
    public async Task EmExecucao_ExecucaoInviavel_DeveEstornarOPagamentoSemCancelarAOsAinda()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmExecucaoAsync();

        await cenario.PublicarAsync(new ExecucaoInviavelBuilder().DaOrdem(cenario.OrdemId).ComMotivo("Peça descontinuada.").Build());
        await cenario.AguardarEstadoAsync(m => m.Estornando);

        var comando = cenario.EnviadosDoTipo<EstornarPagamento>().Should().ContainSingle().Subject;
        comando.PagamentoId.Should().Be(cenario.PagamentoId);
        comando.Motivo.Should().Be("Execução inviável: Peça descontinuada.");
        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.EmExecucao, "o dinheiro ainda nao voltou");
        ordem.EstoqueReservado.Should().BeTrue();
    }

    [Fact]
    public async Task Estornando_PagamentoEstornado_DeveLiberarOEstoqueECancelarAOsComOMotivoDaInviabilidade()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmEstornandoAsync();

        await cenario.PublicarAsync(new PagamentoEstornadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be("Execução inviável: Peça do fabricante descontinuada.");
        ordem.EstoqueReservado.Should().BeFalse();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
    }

    [Fact]
    public async Task Estornando_EstornoFalhou_DeveIrParaEstornoPendenteManualSemCancelarNemLiberarEAlertar()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmEstornandoAsync();

        await cenario.PublicarAsync(new EstornoFalhouBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).ComErro("Gateway indisponível.").Build());
        await cenario.AguardarEstadoAsync(m => m.EstornoPendenteManual);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.EmExecucao, "EstornoPendenteManual nao vira Cancelada");
        ordem.EstoqueReservado.Should().BeTrue();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(EstoqueDepoisDaReserva);
        cenario.Ambiente.Logs.Registros.Should().ContainSingle(r =>
            r.Nivel == LogLevel.Error && r.Mensagem.Contains("EstornoPendenteManual") && r.Mensagem.Contains("Gateway indisponível.")
            && r.Mensagem.Contains(cenario.OrdemId.ToString()));
    }

    [Fact]
    public async Task EstornoPendenteManual_PagamentoEstornadoPeloStaff_DeveLiberarOEstoqueECancelarAOs()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmEstornoPendenteManualAsync();

        await cenario.PublicarAsync(new PagamentoEstornadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.AguardarEstadoAsync(m => m.Cancelada);

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.Cancelada);
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
    }

    [Fact]
    public async Task EstornoPendenteManual_NovoEstornoFalhou_DeveContinuarPendenteSemNovoAlerta()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmEstornoPendenteManualAsync();
        var alertas = cenario.Ambiente.Logs.Registros.Count;

        await cenario.PublicarIgnoradoAsync(new EstornoFalhouBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());

        cenario.Ambiente.Logs.Registros.Count.Should().Be(alertas);
    }
}
