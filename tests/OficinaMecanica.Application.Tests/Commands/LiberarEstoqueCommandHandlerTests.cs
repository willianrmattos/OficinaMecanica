using FluentAssertions;
using OficinaMecanica.Application.Commands.LiberarEstoque;
using OficinaMecanica.Application.Commands.ReservarEstoque;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class LiberarEstoqueCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IPecaRepository> _pecaRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly LiberarEstoqueCommandHandler _handler;

    public LiberarEstoqueCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new LiberarEstoqueCommandHandler(
            _ordemRepositoryMock.Object, _pecaRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    private Peca ConfigurarPeca(int estoque)
    {
        var peca = new PecaBuilder().ComEstoque(estoque).Build();
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(peca);
        return peca;
    }

    [Fact]
    public async Task Handle_OrdemComReserva_DeveDevolverExatamenteAQuantidadeDeCadaPeca()
    {
        var pecaA = ConfigurarPeca(7);
        var pecaB = ConfigurarPeca(3);
        var ordem = new OrdemDeServicoBuilder()
            .ComPeca(pecaA.Id, pecaA.Nome, pecaA.PrecoUnitario, 3)
            .ComPeca(pecaB.Id, pecaB.Nome, pecaB.PrecoUnitario, 2)
            .ComEstoqueReservado()
            .AguardandoPagamento();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new LiberarEstoqueCommand(ordem.Id), CancellationToken.None);

        pecaA.QuantidadeEstoque.Should().Be(10);
        pecaB.QuantidadeEstoque.Should().Be(5);
        ordem.EstoqueReservado.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_OrdemSemReserva_DeveSerNoOpSemMexerEmEstoque()
    {
        var peca = ConfigurarPeca(10);
        var ordem = new OrdemDeServicoBuilder().ComPeca(peca.Id, peca.Nome, peca.PrecoUnitario, 3).AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new LiberarEstoqueCommand(ordem.Id), CancellationToken.None);

        peca.QuantidadeEstoque.Should().Be(10);
        _pecaRepositoryMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_ChamadoDuasVezes_DeveDevolverUmaSoVez()
    {
        var peca = ConfigurarPeca(7);
        var ordem = new OrdemDeServicoBuilder()
            .ComPeca(peca.Id, peca.Nome, peca.PrecoUnitario, 3)
            .ComEstoqueReservado()
            .AguardandoPagamento();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new LiberarEstoqueCommand(ordem.Id), CancellationToken.None);
        await _handler.Handle(new LiberarEstoqueCommand(ordem.Id), CancellationToken.None);

        peca.QuantidadeEstoque.Should().Be(10);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_DepoisDeReservar_DeveRestaurarOEstoqueOriginal()
    {
        var peca = ConfigurarPeca(10);
        var ordem = new OrdemDeServicoBuilder().ComPeca(peca.Id, peca.Nome, peca.PrecoUnitario, 4).AguardandoAprovacao();
        ConfigurarOrdem(ordem);
        var reservar = new ReservarEstoqueCommandHandler(_ordemRepositoryMock.Object, _pecaRepositoryMock.Object, _unitOfWorkMock.Object);
        await reservar.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);
        peca.QuantidadeEstoque.Should().Be(6);

        await _handler.Handle(new LiberarEstoqueCommand(ordem.Id), CancellationToken.None);

        peca.QuantidadeEstoque.Should().Be(10);
        ordem.EstoqueReservado.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OrdemCanceladaComReserva_DeveDevolverOEstoque()
    {
        var peca = ConfigurarPeca(7);
        var ordem = new OrdemDeServicoBuilder()
            .ComPeca(peca.Id, peca.Nome, peca.PrecoUnitario, 3)
            .ComEstoqueReservado()
            .Cancelada();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new LiberarEstoqueCommand(ordem.Id), CancellationToken.None);

        peca.QuantidadeEstoque.Should().Be(10);
        ordem.EstoqueReservado.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PecaNaoEncontradaNoSistema_DeveLancarExcecaoSemDevolverAsDemais()
    {
        var pecaExistente = ConfigurarPeca(7);
        var ordem = new OrdemDeServicoBuilder()
            .ComPeca(pecaExistente.Id, pecaExistente.Nome, pecaExistente.PrecoUnitario, 3)
            .ComPeca(Guid.NewGuid(), "Filtro Inexistente", 50m, 1)
            .ComEstoqueReservado()
            .AguardandoPagamento();
        ConfigurarOrdem(ordem);
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.Is<Guid>(id => id != pecaExistente.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);

        var act = () => _handler.Handle(new LiberarEstoqueCommand(ordem.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada no sistema*");
        pecaExistente.QuantidadeEstoque.Should().Be(7);
        ordem.EstoqueReservado.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new LiberarEstoqueCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
