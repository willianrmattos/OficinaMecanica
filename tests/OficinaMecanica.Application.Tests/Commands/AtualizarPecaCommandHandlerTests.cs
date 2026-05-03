using FluentAssertions;
using OficinaMecanica.Application.Commands.AtualizarPeca;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class AtualizarPecaCommandHandlerTests
{
    private readonly Mock<IPecaRepository> _pecaRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AtualizarPecaCommandHandler _handler;

    public AtualizarPecaCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new AtualizarPecaCommandHandler(_pecaRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_PecaExistente_DeveAtualizarESalvar()
    {
        var peca = new PecaBuilder().ComNome("Filtro de Ar").ComCodigo("FA-001").ComPreco(25m).ComEstoqueMinimo(2).Build();
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(peca);

        var command = new AtualizarPecaCommand(Guid.NewGuid(), "Filtro de Óleo", "Filtro para motor", "FO-002", 35m, 5);
        await _handler.Handle(command, CancellationToken.None);

        peca.Nome.Should().Be("Filtro de Óleo");
        peca.PrecoUnitario.Should().Be(35m);
        peca.EstoqueMinimo.Should().Be(5);
        _pecaRepositoryMock.Verify(r => r.Atualizar(peca), Times.Once());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_PecaNaoEncontrada_DeveLancarExcecao()
    {
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);

        var act = () => _handler.Handle(new AtualizarPecaCommand(Guid.NewGuid(), "Nome", null, null, 10m, 1), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
