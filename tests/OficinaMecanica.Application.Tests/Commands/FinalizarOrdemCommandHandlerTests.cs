using FluentAssertions;
using OficinaMecanica.Application.Commands.FinalizarOrdem;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class FinalizarOrdemCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly FinalizarOrdemCommandHandler _handler;

    public FinalizarOrdemCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new FinalizarOrdemCommandHandler(_ordemRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Fact]
    public async Task Handle_OrdemEmExecucao_DeveFinalizarRegistrarConclusaoESalvar()
    {
        var ordem = new OrdemDeServicoBuilder().EmExecucao();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new FinalizarOrdemCommand(ordem.Id), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        ordem.DataConclusao.Should().NotBeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Entregue)]
    public async Task Handle_OrdemJaFinalizada_DeveSerNoOpSemAlterarADataDeConclusao(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        var conclusaoAntes = ordem.DataConclusao;
        var historicoAntes = ordem.HistoricoStatus.Count;
        ConfigurarOrdem(ordem);

        await _handler.Handle(new FinalizarOrdemCommand(ordem.Id), CancellationToken.None);

        ordem.Status.Should().Be(status);
        ordem.DataConclusao.Should().Be(conclusaoAntes);
        ordem.HistoricoStatus.Should().HaveCount(historicoAntes);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_ChamadoDuasVezes_DeveFinalizarUmaSoVez()
    {
        var ordem = new OrdemDeServicoBuilder().EmExecucao();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new FinalizarOrdemCommand(ordem.Id), CancellationToken.None);
        await _handler.Handle(new FinalizarOrdemCommand(ordem.Id), CancellationToken.None);

        ordem.HistoricoStatus.Count(h => h.Status == StatusOrdemDeServico.Finalizada).Should().Be(1);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public async Task Handle_OrdemQueNaoEstaEmExecucao_DeveLancarExcecaoSemSalvar(StatusOrdemDeServico status)
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().NoStatus(status));

        var act = () => _handler.Handle(new FinalizarOrdemCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Não é possível*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new FinalizarOrdemCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
