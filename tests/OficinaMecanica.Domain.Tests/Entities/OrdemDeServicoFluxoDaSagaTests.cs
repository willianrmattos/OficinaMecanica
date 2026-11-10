using FluentAssertions;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.Tests.Entities;

public class OrdemDeServicoFluxoDaSagaTests
{
    private static readonly DateTimeOffset Prazo = new(2026, 3, 12, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ParticipaDaSaga_OrdemCriadaPeloConstrutor_DeveSerVerdadeira()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();

        ordem.ParticipaDaSaga.Should().BeTrue();
    }

    [Fact]
    public void ParticipaDaSaga_OrdemAnteriorASaga_DeveSerFalsa()
    {
        var ordem = new OrdemDeServicoBuilder().AnteriorASaga().AguardandoAprovacao();

        ordem.ParticipaDaSaga.Should().BeFalse();
    }

    [Fact]
    public void AprovarOrcamento_DeAguardandoAprovacao_DeveIrParaAguardandoPagamento()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ordem.ClearDomainEvents();

        ordem.AprovarOrcamento();

        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        ordem.HistoricoStatus.Last().Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        ordem.HistoricoStatus.Last().Observacao.Should().Contain("Aguardando pagamento");
        ordem.DomainEvents.OfType<OrcamentoAprovadoEvent>().Should().ContainSingle()
            .Which.OrdemDeServicoId.Should().Be(ordem.Id);
        ordem.DomainEvents.OfType<StatusOrdemAlteradoEvent>().Should().ContainSingle()
            .Which.NovoStatus.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public void AprovarOrcamento_DeOutroStatus_DeveLancarExcecao(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        var act = () => ordem.AprovarOrcamento();

        act.Should().Throw<DomainException>().WithMessage("*Não é possível*");
    }

    [Fact]
    public void IniciarExecucao_DeAguardandoPagamento_DeveIrParaEmExecucao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ordem.ClearDomainEvents();

        ordem.IniciarExecucao();

        ordem.Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        ordem.HistoricoStatus.Last().Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        ordem.DomainEvents.OfType<StatusOrdemAlteradoEvent>().Should().ContainSingle()
            .Which.NovoStatus.Should().Be(StatusOrdemDeServico.EmExecucao);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public void IniciarExecucao_DeOutroStatus_DeveLancarExcecao(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        var act = () => ordem.IniciarExecucao();

        act.Should().Throw<DomainException>().WithMessage("*Não é possível*");
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    public void Cancelar_DeStatusPermitido_DeveCancelarERegistrarMotivo(StatusOrdemDeServico origem)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(origem);
        ordem.ClearDomainEvents();

        ordem.Cancelar("Cliente desistiu do serviço.");

        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be("Cliente desistiu do serviço.");
        ordem.HistoricoStatus.Last().Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.HistoricoStatus.Last().Observacao.Should().Contain("Cliente desistiu do serviço.");
        ordem.DomainEvents.OfType<StatusOrdemAlteradoEvent>().Should().ContainSingle()
            .Which.NovoStatus.Should().Be(StatusOrdemDeServico.Cancelada);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Entregue)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public void Cancelar_DeStatusSemTransicao_DeveLancarExcecaoESemAlterarMotivo(StatusOrdemDeServico origem)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(origem);

        var act = () => ordem.Cancelar("Tentativa tardia.");

        act.Should().Throw<DomainException>().WithMessage("*Não é possível*");
        ordem.Status.Should().Be(origem);
        ordem.MotivoCancelamento.Should().BeNull();
    }

    [Fact]
    public void Cancelar_JaCancelada_DeveSerIdempotenteEManterOMotivoOriginal()
    {
        var ordem = new OrdemDeServicoBuilder().Cancelada(motivo: "Primeiro motivo.");
        var historicoAntes = ordem.HistoricoStatus.Count;
        ordem.ClearDomainEvents();

        ordem.Cancelar("Segundo motivo.");

        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be("Primeiro motivo.");
        ordem.HistoricoStatus.Should().HaveCount(historicoAntes);
        ordem.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancelar_SemMotivo_DeveLancarExcecao(string? motivo)
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();

        var act = () => ordem.Cancelar(motivo!);

        act.Should().Throw<DomainException>().WithMessage("*motivo do cancelamento é obrigatório*");
        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
    }

    [Fact]
    public void Cancelar_MotivoMaiorQueOLimite_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();

        var act = () => ordem.Cancelar(new string('x', OrdemDeServico.TamanhoMaximoDoMotivo + 1));

        act.Should().Throw<DomainException>().WithMessage("*no máximo*");
    }

    [Fact]
    public void Cancelar_MotivoNoLimiteComEspacosNasPontas_DeveGravarSemOsEspacos()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        var motivo = new string('x', OrdemDeServico.TamanhoMaximoDoMotivo);

        ordem.Cancelar($"  {motivo}  ");

        ordem.MotivoCancelamento.Should().Be(motivo);
        ordem.HistoricoStatus.Last().Observacao.Length.Should().BeLessThanOrEqualTo(500);
    }

    [Fact]
    public void RegistrarPagamentoSolicitado_EmAguardandoPagamento_DeveGravarDadosELevantarEvento()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var pagamentoId = Guid.NewGuid();
        ordem.ClearDomainEvents();

        ordem.RegistrarPagamentoSolicitado(pagamentoId, "https://pagamento.exemplo/checkout/123", Prazo);

        ordem.PagamentoId.Should().Be(pagamentoId);
        ordem.LinkPagamento.Should().Be("https://pagamento.exemplo/checkout/123");
        ordem.PagamentoExpiraEm.Should().Be(Prazo);
        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        var evento = ordem.DomainEvents.OfType<PagamentoSolicitadoEvent>().Should().ContainSingle().Subject;
        evento.OrdemDeServicoId.Should().Be(ordem.Id);
        evento.ClienteId.Should().Be(ordem.ClienteId);
        evento.LinkPagamento.Should().Be("https://pagamento.exemplo/checkout/123");
        evento.ExpiraEm.Should().Be(Prazo);
        ordem.DomainEvents.OfType<StatusOrdemAlteradoEvent>().Should().BeEmpty();
    }

    [Fact]
    public void RegistrarPagamentoSolicitado_MesmoPagamentoDeNovo_DeveSerIdempotente()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var pagamentoId = Guid.NewGuid();
        ordem.RegistrarPagamentoSolicitado(pagamentoId, "https://pagamento.exemplo/checkout/123", Prazo);
        ordem.ClearDomainEvents();

        ordem.RegistrarPagamentoSolicitado(pagamentoId, "https://pagamento.exemplo/checkout/123", Prazo);

        ordem.DomainEvents.Should().BeEmpty();
        ordem.PagamentoId.Should().Be(pagamentoId);
    }

    [Fact]
    public void RegistrarPagamentoSolicitado_ReentregaDepoisDeAOrdemAvancar_DeveSerIdempotente()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var pagamentoId = Guid.NewGuid();
        ordem.RegistrarPagamentoSolicitado(pagamentoId, "https://pagamento.exemplo/checkout/123", Prazo);
        ordem.IniciarExecucao();
        ordem.ClearDomainEvents();

        var act = () => ordem.RegistrarPagamentoSolicitado(pagamentoId, "https://pagamento.exemplo/checkout/123", Prazo);

        act.Should().NotThrow();
        ordem.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RegistrarPagamentoSolicitado_OutroPagamentoNaMesmaOrdem_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var primeiro = Guid.NewGuid();
        ordem.RegistrarPagamentoSolicitado(primeiro, "https://pagamento.exemplo/checkout/123", Prazo);

        var act = () => ordem.RegistrarPagamentoSolicitado(Guid.NewGuid(), "https://pagamento.exemplo/checkout/456", Prazo);

        act.Should().Throw<DomainException>().WithMessage("*outro pagamento*");
        ordem.PagamentoId.Should().Be(primeiro);
        ordem.LinkPagamento.Should().Be("https://pagamento.exemplo/checkout/123");
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public void RegistrarPagamentoSolicitado_ForaDeAguardandoPagamento_DeveLancarExcecao(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        var act = () => ordem.RegistrarPagamentoSolicitado(Guid.NewGuid(), "https://pagamento.exemplo/checkout/123", Prazo);

        act.Should().Throw<DomainException>().WithMessage("*AguardandoPagamento*");
        ordem.PagamentoId.Should().BeNull();
    }

    [Fact]
    public void RegistrarPagamentoSolicitado_ComIdVazio_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();

        var act = () => ordem.RegistrarPagamentoSolicitado(Guid.Empty, "https://pagamento.exemplo/checkout/123", Prazo);

        act.Should().Throw<DomainException>().WithMessage("*identificador do pagamento*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void RegistrarPagamentoSolicitado_SemLink_DeveLancarExcecao(string? link)
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();

        var act = () => ordem.RegistrarPagamentoSolicitado(Guid.NewGuid(), link!, Prazo);

        act.Should().Throw<DomainException>().WithMessage("*link de pagamento é obrigatório*");
    }

    [Fact]
    public void RegistrarPagamentoSolicitado_LinkMaiorQueOLimite_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var link = "https://pagamento.exemplo/" + new string('a', OrdemDeServico.TamanhoMaximoDoLinkDePagamento);

        var act = () => ordem.RegistrarPagamentoSolicitado(Guid.NewGuid(), link, Prazo);

        act.Should().Throw<DomainException>().WithMessage("*no máximo*");
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    public void ReservarEstoque_EmStatusPermitido_DeveMarcarOFlag(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        ordem.ReservarEstoque();

        ordem.EstoqueReservado.Should().BeTrue();
    }

    [Fact]
    public void ReservarEstoque_JaReservado_DeveSerIdempotente()
    {
        var ordem = new OrdemDeServicoBuilder().ComEstoqueReservado().AguardandoAprovacao();

        var act = () => ordem.ReservarEstoque();

        act.Should().NotThrow();
        ordem.EstoqueReservado.Should().BeTrue();
    }

    [Fact]
    public void ReservarEstoque_JaReservadoEOrdemAvancou_DeveSerIdempotenteSemValidarStatus()
    {
        var ordem = new OrdemDeServicoBuilder().ComEstoqueReservado().EmExecucao();

        var act = () => ordem.ReservarEstoque();

        act.Should().NotThrow();
        ordem.EstoqueReservado.Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public void ReservarEstoque_EmStatusNaoPermitido_DeveLancarExcecao(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        var act = () => ordem.ReservarEstoque();

        act.Should().Throw<DomainException>().WithMessage("*reservar o estoque*");
        ordem.EstoqueReservado.Should().BeFalse();
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    public void ValidarPodeReservarEstoque_EmStatusPermitido_NaoDeveLancarNemAlterarAOrdem(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        var act = () => ordem.ValidarPodeReservarEstoque();

        act.Should().NotThrow();
        ordem.EstoqueReservado.Should().BeFalse();
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public void ValidarPodeReservarEstoque_EmStatusNaoPermitido_DeveLancarExcecao(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        var act = () => ordem.ValidarPodeReservarEstoque();

        act.Should().Throw<DomainException>().WithMessage("*reservar o estoque*");
    }

    [Fact]
    public void LiberarEstoque_ComReserva_DeveLimparOFlag()
    {
        var ordem = new OrdemDeServicoBuilder().ComEstoqueReservado().AguardandoPagamento();

        ordem.LiberarEstoque();

        ordem.EstoqueReservado.Should().BeFalse();
    }

    [Fact]
    public void LiberarEstoque_SemReserva_DeveSerIdempotente()
    {
        var ordem = new OrdemDeServicoBuilder().Cancelada();

        var act = () => ordem.LiberarEstoque();

        act.Should().NotThrow();
        ordem.EstoqueReservado.Should().BeFalse();
    }

    [Fact]
    public void LiberarEstoque_DeOrdemJaCancelada_DeveLimparOFlag()
    {
        var ordem = new OrdemDeServicoBuilder().ComEstoqueReservado().Cancelada();

        ordem.LiberarEstoque();

        ordem.EstoqueReservado.Should().BeFalse();
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida, StatusOrdemDeServico.Recebida, true)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico, StatusOrdemDeServico.Recebida, true)]
    [InlineData(StatusOrdemDeServico.Recebida, StatusOrdemDeServico.EmDiagnostico, false)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento, StatusOrdemDeServico.AguardandoAprovacao, true)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao, StatusOrdemDeServico.AguardandoPagamento, false)]
    [InlineData(StatusOrdemDeServico.EmExecucao, StatusOrdemDeServico.AguardandoPagamento, true)]
    [InlineData(StatusOrdemDeServico.Entregue, StatusOrdemDeServico.EmExecucao, true)]
    [InlineData(StatusOrdemDeServico.Cancelada, StatusOrdemDeServico.EmDiagnostico, false)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado, StatusOrdemDeServico.Recebida, false)]
    [InlineData(StatusOrdemDeServico.EmExecucao, StatusOrdemDeServico.Cancelada, false)]
    public void JaAlcancou_DeveConsiderarSoOCaminhoPrincipal(StatusOrdemDeServico atual, StatusOrdemDeServico alvo, bool esperado)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(atual);

        ordem.JaAlcancou(alvo).Should().Be(esperado);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida, true)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico, true)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao, true)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento, true)]
    [InlineData(StatusOrdemDeServico.EmExecucao, false)]
    [InlineData(StatusOrdemDeServico.Finalizada, false)]
    [InlineData(StatusOrdemDeServico.Entregue, false)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado, false)]
    [InlineData(StatusOrdemDeServico.Cancelada, false)]
    public void PodeSerCanceladaAntesDaExecucao_DeveValerSoParaOsStatusAnterioresAExecucao(StatusOrdemDeServico status, bool esperado)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        ordem.PodeSerCanceladaAntesDaExecucao.Should().Be(esperado);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao, true)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento, false)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado, false)]
    [InlineData(StatusOrdemDeServico.Cancelada, false)]
    public void AguardaDecisaoDoCliente_DeveValerSoEmAguardandoAprovacao(StatusOrdemDeServico status, bool esperado)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        ordem.AguardaDecisaoDoCliente.Should().Be(esperado);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado, 0)]
    [InlineData(StatusOrdemDeServico.Recebida, 1)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico, 2)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao, 3)]
    [InlineData(StatusOrdemDeServico.EmExecucao, 4)]
    [InlineData(StatusOrdemDeServico.Finalizada, 5)]
    [InlineData(StatusOrdemDeServico.Entregue, 6)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento, 7)]
    [InlineData(StatusOrdemDeServico.Cancelada, 8)]
    public void Status_ValoresPersistidosNaoPodemSerRenumerados(StatusOrdemDeServico status, int valor)
    {
        ((int)status).Should().Be(valor);
    }

    [Fact]
    public void Finalizar_DeAguardandoPagamento_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();

        var act = () => ordem.Finalizar();

        act.Should().Throw<DomainException>().WithMessage("*Não é possível*");
    }

    [Fact]
    public void Entregar_DeEmExecucao_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().EmExecucao();

        var act = () => ordem.Entregar();

        act.Should().Throw<DomainException>().WithMessage("*Não é possível*");
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public void AvancarParaDiagnostico_DeStatusTerminalAlternativo_DeveLancarExcecao(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);

        var act = () => ordem.AvancarParaDiagnostico();

        act.Should().Throw<DomainException>().WithMessage("*Não é possível*");
    }

    [Fact]
    public void AdicionarPeca_EmAguardandoPagamento_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();

        var act = () => ordem.AdicionarPeca(Guid.NewGuid(), "Filtro", 40m, 1);

        act.Should().Throw<DomainException>().WithMessage("*Só é possível adicionar*");
    }
}
