using FluentAssertions;
using OficinaMecanica.Application.Commands.AplicarRecusaDoOrcamento;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class AplicarRecusaDoOrcamentoCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AplicarRecusaDoOrcamentoCommandHandler _handler;

    public AplicarRecusaDoOrcamentoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new AplicarRecusaDoOrcamentoCommandHandler(_ordemRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Fact]
    public async Task Handle_OrdemAguardandoAprovacao_DeveRecusarComMotivoESalvar()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ordem.ClearDomainEvents();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AplicarRecusaDoOrcamentoCommand(ordem.Id, "Achei caro."), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.OrcamentoRecusado);
        ordem.HistoricoStatus.Last().Observacao.Should().Contain("Achei caro.");
        ordem.DomainEvents.OfType<OrcamentoRecusadoEvent>().Should().ContainSingle()
            .Which.Motivo.Should().Be("Achei caro.");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_SemMotivo_DeveRecusarComTextoPadrao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AplicarRecusaDoOrcamentoCommand(ordem.Id), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.OrcamentoRecusado);
        ordem.HistoricoStatus.Last().Observacao.Should().Be("Orçamento recusado pelo cliente.");
    }

    [Fact]
    public async Task Handle_OrdemJaRecusada_DeveSerNoOp()
    {
        var ordem = new OrdemDeServicoBuilder().OrcamentoRecusado();
        var historicoAntes = ordem.HistoricoStatus.Count;
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AplicarRecusaDoOrcamentoCommand(ordem.Id, "Outro motivo."), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.OrcamentoRecusado);
        ordem.HistoricoStatus.Should().HaveCount(historicoAntes);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_ChamadoDuasVezes_DeveRecusarUmaSoVez()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AplicarRecusaDoOrcamentoCommand(ordem.Id), CancellationToken.None);
        await _handler.Handle(new AplicarRecusaDoOrcamentoCommand(ordem.Id), CancellationToken.None);

        ordem.HistoricoStatus.Count(h => h.Status == StatusOrdemDeServico.OrcamentoRecusado).Should().Be(1);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public async Task Handle_OrdemQueNaoPodeSerRecusada_DeveLancarExcecaoSemSalvar(StatusOrdemDeServico status)
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().NoStatus(status));

        var act = () => _handler.Handle(new AplicarRecusaDoOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Não é possível*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new AplicarRecusaDoOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
