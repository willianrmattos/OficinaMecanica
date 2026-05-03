using FluentAssertions;
using OficinaMecanica.Application.Commands.AdicionarEstoquePeca;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class AdicionarEstoquePecaCommandHandlerTests
{
    private readonly Mock<IPecaRepository> _pecaRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AdicionarEstoquePecaCommandHandler _handler;

    public AdicionarEstoquePecaCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new AdicionarEstoquePecaCommandHandler(_pecaRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_QuantidadeValida_DeveIncrementarEstoque()
    {
        var peca = new PecaBuilder().ComEstoque(5).Build();
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(peca);

        var result = await _handler.Handle(new AdicionarEstoquePecaCommand(Guid.NewGuid(), 10), CancellationToken.None);

        result.QuantidadeEstoque.Should().Be(15);
        _pecaRepositoryMock.Verify(r => r.Atualizar(peca), Times.Once());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_PecaNaoEncontrada_DeveLancarExcecao()
    {
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);

        var act = () => _handler.Handle(new AdicionarEstoquePecaCommand(Guid.NewGuid(), 5), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task Handle_QuantidadeZero_DeveLancarExcecao()
    {
        var peca = new PecaBuilder().ComEstoque(5).Build();
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(peca);

        var act = () => _handler.Handle(new AdicionarEstoquePecaCommand(Guid.NewGuid(), 0), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*maior que zero*");
    }
}
