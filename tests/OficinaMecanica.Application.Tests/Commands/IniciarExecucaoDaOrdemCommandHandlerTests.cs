using FluentAssertions;
using OficinaMecanica.Application.Commands.IniciarExecucaoDaOrdem;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class IniciarExecucaoDaOrdemCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly IniciarExecucaoDaOrdemCommandHandler _handler;

    public IniciarExecucaoDaOrdemCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new IniciarExecucaoDaOrdemCommandHandler(_ordemRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Fact]
    public async Task Handle_OrdemAguardandoPagamento_DeveIrParaEmExecucaoESalvar()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new IniciarExecucaoDaOrdemCommand(ordem.Id), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Entregue)]
    public async Task Handle_OrdemQueJaEstaEmExecucaoOuDepois_DeveSerNoOp(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        var historicoAntes = ordem.HistoricoStatus.Count;
        ConfigurarOrdem(ordem);

        await _handler.Handle(new IniciarExecucaoDaOrdemCommand(ordem.Id), CancellationToken.None);

        ordem.Status.Should().Be(status);
        ordem.HistoricoStatus.Should().HaveCount(historicoAntes);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_ChamadoDuasVezes_DeveIniciarUmaSoVez()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new IniciarExecucaoDaOrdemCommand(ordem.Id), CancellationToken.None);
        await _handler.Handle(new IniciarExecucaoDaOrdemCommand(ordem.Id), CancellationToken.None);

        ordem.HistoricoStatus.Count(h => h.Status == StatusOrdemDeServico.EmExecucao).Should().Be(1);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public async Task Handle_OrdemSemPagamentoConfirmado_DeveLancarExcecaoSemSalvar(StatusOrdemDeServico status)
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().NoStatus(status));

        var act = () => _handler.Handle(new IniciarExecucaoDaOrdemCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Não é possível*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new IniciarExecucaoDaOrdemCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
