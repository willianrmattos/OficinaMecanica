using FluentAssertions;
using OficinaMecanica.Application.Commands.AtualizarStatusOrdem;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class AtualizarStatusOrdemCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AtualizarStatusOrdemCommandHandler _handler;

    public AtualizarStatusOrdemCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new AtualizarStatusOrdemCommandHandler(_ordemRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Fact]
    public async Task Handle_OrdemFinalizada_DeveEntregarESalvar()
    {
        var ordem = new OrdemDeServicoBuilder().Finalizada();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AtualizarStatusOrdemCommand(ordem.Id, (int)StatusOrdemDeServico.Entregue), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.Entregue);
        ordem.HistoricoStatus.Last().Status.Should().Be(StatusOrdemDeServico.Entregue);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public async Task Handle_OrdemQueAindaNaoFoiFinalizada_DeveLancarExcecaoSemSalvar(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        ConfigurarOrdem(ordem);

        var act = () => _handler.Handle(new AtualizarStatusOrdemCommand(ordem.Id, (int)StatusOrdemDeServico.Entregue), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Não é possível alterar*");
        ordem.Status.Should().Be(status);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)StatusOrdemDeServico.Entregue), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }
}
