using FluentAssertions;
using OficinaMecanica.Application.Commands.CancelarOrdemPelaSaga;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class CancelarOrdemPelaSagaCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CancelarOrdemPelaSagaCommandHandler _handler;

    public CancelarOrdemPelaSagaCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new CancelarOrdemPelaSagaCommandHandler(_ordemRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    public async Task Handle_OrdemCancelavel_DeveCancelarComMotivoESalvar(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        ordem.ClearDomainEvents();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new CancelarOrdemPelaSagaCommand(ordem.Id, "Pagamento não realizado no prazo."), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be("Pagamento não realizado no prazo.");
        ordem.DomainEvents.OfType<StatusOrdemAlteradoEvent>().Should().ContainSingle()
            .Which.NovoStatus.Should().Be(StatusOrdemDeServico.Cancelada);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_SemMotivo_DeveCancelarComOMotivoPadrao(string? motivo)
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new CancelarOrdemPelaSagaCommand(ordem.Id, motivo), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be(CancelarOrdemPelaSagaCommandHandler.MotivoPadrao);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_MotivoMaiorQueOLimite_DeveTruncarECancelarEmVezDeRejeitar()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ConfigurarOrdem(ordem);
        var motivoLongo = new string('a', OrdemDeServico.TamanhoMaximoDoMotivo) + "cauda que deve sumir";

        await _handler.Handle(new CancelarOrdemPelaSagaCommand(ordem.Id, motivoLongo), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be(new string('a', OrdemDeServico.TamanhoMaximoDoMotivo));
        ordem.HistoricoStatus.Last().Observacao.Length.Should().BeLessThanOrEqualTo(500);
    }

    [Fact]
    public async Task Handle_MotivoCujoCorteCaiNoMeioDeUmEmoji_NaoDeveGravarMeioCaractere()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ConfigurarOrdem(ordem);
        var motivo = new string('a', OrdemDeServico.TamanhoMaximoDoMotivo - 1) + "🔧 depois do emoji";

        await _handler.Handle(new CancelarOrdemPelaSagaCommand(ordem.Id, motivo), CancellationToken.None);

        ordem.MotivoCancelamento.Should().Be(new string('a', OrdemDeServico.TamanhoMaximoDoMotivo - 1));
    }

    [Fact]
    public async Task Handle_MotivoNoLimiteComEspacosNasPontas_DeveManterOTextoSemOsEspacos()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ConfigurarOrdem(ordem);
        var motivo = new string('b', OrdemDeServico.TamanhoMaximoDoMotivo);

        await _handler.Handle(new CancelarOrdemPelaSagaCommand(ordem.Id, $"   {motivo}   "), CancellationToken.None);

        ordem.MotivoCancelamento.Should().Be(motivo);
    }

    [Fact]
    public async Task Handle_NaoDeveMexerNaReservaDeEstoque()
    {
        var ordem = new OrdemDeServicoBuilder().ComEstoqueReservado().AguardandoPagamento();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new CancelarOrdemPelaSagaCommand(ordem.Id, "Cancelada."), CancellationToken.None);

        ordem.EstoqueReservado.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OrdemJaCancelada_DeveSerNoOpEManterOMotivoOriginal()
    {
        var ordem = new OrdemDeServicoBuilder().Cancelada(motivo: "Primeiro motivo.");
        var historicoAntes = ordem.HistoricoStatus.Count;
        ConfigurarOrdem(ordem);

        await _handler.Handle(new CancelarOrdemPelaSagaCommand(ordem.Id, "Segundo motivo."), CancellationToken.None);

        ordem.MotivoCancelamento.Should().Be("Primeiro motivo.");
        ordem.HistoricoStatus.Should().HaveCount(historicoAntes);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_ChamadoDuasVezes_DeveCancelarUmaSoVez()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new CancelarOrdemPelaSagaCommand(ordem.Id, "Motivo."), CancellationToken.None);
        await _handler.Handle(new CancelarOrdemPelaSagaCommand(ordem.Id, "Motivo."), CancellationToken.None);

        ordem.HistoricoStatus.Count(h => h.Status == StatusOrdemDeServico.Cancelada).Should().Be(1);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Entregue)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public async Task Handle_OrdemSemTransicaoParaCancelada_DeveLancarExcecaoSemSalvar(StatusOrdemDeServico status)
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().NoStatus(status));

        var act = () => _handler.Handle(new CancelarOrdemPelaSagaCommand(Guid.NewGuid(), "Motivo."), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Não é possível*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new CancelarOrdemPelaSagaCommand(Guid.NewGuid(), "Motivo."), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
