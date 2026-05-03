using FluentAssertions;
using OficinaMecanica.Application.Commands.DesativarPeca;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class DesativarPecaCommandHandlerTests
{
    private readonly Mock<IPecaRepository> _pecaRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DesativarPecaCommandHandler _handler;

    public DesativarPecaCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new DesativarPecaCommandHandler(_pecaRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_Desativar_DeveMarcarInativo()
    {
        var peca = new PecaBuilder().Build();
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(peca);

        await _handler.Handle(new DesativarPecaCommand(Guid.NewGuid(), Ativar: false), CancellationToken.None);

        peca.Ativo.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_Ativar_DeveMarcarAtivo()
    {
        var peca = new PecaBuilder().Build();
        peca.Desativar();
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(peca);

        await _handler.Handle(new DesativarPecaCommand(Guid.NewGuid(), Ativar: true), CancellationToken.None);

        peca.Ativo.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_PecaNaoEncontrada_DeveLancarExcecao()
    {
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);

        var act = () => _handler.Handle(new DesativarPecaCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
