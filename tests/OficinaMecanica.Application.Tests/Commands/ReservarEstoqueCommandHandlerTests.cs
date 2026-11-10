using FluentAssertions;
using OficinaMecanica.Application.Commands.ReservarEstoque;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class ReservarEstoqueCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IPecaRepository> _pecaRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ReservarEstoqueCommandHandler _handler;

    public ReservarEstoqueCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new ReservarEstoqueCommandHandler(
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

    private static OrdemDeServicoBuilder OrdemQueUsa(params (Peca Peca, int Quantidade)[] itens)
    {
        var builder = new OrdemDeServicoBuilder();
        foreach (var (peca, quantidade) in itens)
            builder.ComPeca(peca.Id, peca.Nome, peca.PrecoUnitario, quantidade);
        return builder;
    }

    [Fact]
    public async Task Handle_EstoqueSuficiente_DeveBaixarCadaPecaMarcarOFlagESalvarUmaVez()
    {
        var pecaA = ConfigurarPeca(10);
        var pecaB = ConfigurarPeca(5);
        var ordem = OrdemQueUsa((pecaA, 3), (pecaB, 2)).AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        var resultado = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        resultado.Should().Be(ResultadoDaReservaDeEstoque.Reservado);
        pecaA.QuantidadeEstoque.Should().Be(7);
        pecaB.QuantidadeEstoque.Should().Be(3);
        ordem.EstoqueReservado.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_OrdemEmAguardandoPagamento_DevePoderReservar()
    {
        var peca = ConfigurarPeca(4);
        var ordem = OrdemQueUsa((peca, 4)).AguardandoPagamento();
        ConfigurarOrdem(ordem);

        var resultado = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        resultado.Should().Be(ResultadoDaReservaDeEstoque.Reservado);
        peca.QuantidadeEstoque.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OrdemSemPecas_DeveMarcarOFlagSemMexerEmEstoque()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        var resultado = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        resultado.Should().Be(ResultadoDaReservaDeEstoque.Reservado);
        ordem.EstoqueReservado.Should().BeTrue();
        _pecaRepositoryMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_QualquerPecaInsuficiente_NaoDeveReservarNada()
    {
        var pecaSuficiente = ConfigurarPeca(10);
        var pecaInsuficiente = ConfigurarPeca(1);
        var ordem = OrdemQueUsa((pecaSuficiente, 3), (pecaInsuficiente, 2)).AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        var resultado = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        resultado.Should().Be(ResultadoDaReservaDeEstoque.EstoqueInsuficiente);
        pecaSuficiente.QuantidadeEstoque.Should().Be(10);
        pecaInsuficiente.QuantidadeEstoque.Should().Be(1);
        ordem.EstoqueReservado.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_PecaInsuficienteListadaPrimeiro_TambemNaoDeveReservarNada()
    {
        var pecaInsuficiente = ConfigurarPeca(1);
        var pecaSuficiente = ConfigurarPeca(10);
        var ordem = OrdemQueUsa((pecaInsuficiente, 2), (pecaSuficiente, 3)).AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        var resultado = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        resultado.Should().Be(ResultadoDaReservaDeEstoque.EstoqueInsuficiente);
        pecaInsuficiente.QuantidadeEstoque.Should().Be(1);
        pecaSuficiente.QuantidadeEstoque.Should().Be(10);
    }

    [Fact]
    public async Task Handle_EstoqueInsuficiente_DeveSerRepetivelDepoisQueOEstoqueChegar()
    {
        var peca = ConfigurarPeca(1);
        var ordem = OrdemQueUsa((peca, 3)).AguardandoAprovacao();
        ConfigurarOrdem(ordem);
        var primeira = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);
        peca.AdicionarEstoque(5);

        var segunda = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        primeira.Should().Be(ResultadoDaReservaDeEstoque.EstoqueInsuficiente);
        segunda.Should().Be(ResultadoDaReservaDeEstoque.Reservado);
        peca.QuantidadeEstoque.Should().Be(3);
    }

    [Fact]
    public async Task Handle_JaReservado_DeveSerIdempotenteSemBaixarDeNovo()
    {
        var peca = ConfigurarPeca(10);
        var ordem = OrdemQueUsa((peca, 3)).AguardandoAprovacao();
        ConfigurarOrdem(ordem);
        await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        var resultado = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        resultado.Should().Be(ResultadoDaReservaDeEstoque.JaReservado);
        peca.QuantidadeEstoque.Should().Be(7);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_OrdemJaMarcadaComoReservada_NaoDeveConsultarPecas()
    {
        var ordem = new OrdemDeServicoBuilder().ComEstoqueReservado().AguardandoPagamento();
        ConfigurarOrdem(ordem);

        var resultado = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        resultado.Should().Be(ResultadoDaReservaDeEstoque.JaReservado);
        _pecaRepositoryMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_QuantidadeIgualAoEstoque_DeveReservarEZerarEstoque()
    {
        var peca = ConfigurarPeca(3);
        var ordem = OrdemQueUsa((peca, 3)).AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        var resultado = await _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        resultado.Should().Be(ResultadoDaReservaDeEstoque.Reservado);
        peca.QuantidadeEstoque.Should().Be(0);
    }

    [Fact]
    public async Task Handle_StatusNaoPermiteReserva_DeveLancarExcecaoSemBaixarEstoque()
    {
        var peca = ConfigurarPeca(10);
        var ordem = OrdemQueUsa((peca, 3)).EmDiagnostico();
        ConfigurarOrdem(ordem);

        var act = () => _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*reservar o estoque*");
        peca.QuantidadeEstoque.Should().Be(10);
        ordem.EstoqueReservado.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_PecaNaoEncontradaNoSistema_DeveLancarExcecaoSemBaixarAsDemais()
    {
        var pecaExistente = ConfigurarPeca(10);
        var ordem = new OrdemDeServicoBuilder()
            .ComPeca(pecaExistente.Id, pecaExistente.Nome, pecaExistente.PrecoUnitario, 3)
            .ComPeca(Guid.NewGuid(), "Filtro Inexistente", 50m, 1)
            .AguardandoAprovacao();
        ConfigurarOrdem(ordem);
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.Is<Guid>(id => id != pecaExistente.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);

        var act = () => _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada no sistema*");
        pecaExistente.QuantidadeEstoque.Should().Be(10);
        ordem.EstoqueReservado.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new ReservarEstoqueCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task Handle_OrdemCancelada_DeveLancarExcecaoSemBaixarEstoque()
    {
        var peca = ConfigurarPeca(10);
        var ordem = OrdemQueUsa((peca, 3)).Cancelada();
        ConfigurarOrdem(ordem);

        var act = () => _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*reservar o estoque*");
        ordem.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        peca.QuantidadeEstoque.Should().Be(10);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public async Task Handle_StatusNaoPermiteReservaEEstoqueCurto_DeveLancarExcecaoEmVezDeEstoqueInsuficiente(StatusOrdemDeServico status)
    {
        var peca = ConfigurarPeca(1);
        var ordem = OrdemQueUsa((peca, 5)).NoStatus(status);
        ConfigurarOrdem(ordem);

        var act = () => _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*reservar o estoque*");
        peca.QuantidadeEstoque.Should().Be(1);
        ordem.EstoqueReservado.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_StatusNaoPermiteReserva_NaoDeveNemConsultarAsPecas()
    {
        var peca = ConfigurarPeca(10);
        var ordem = OrdemQueUsa((peca, 3)).Cancelada();
        ConfigurarOrdem(ordem);

        var act = () => _handler.Handle(new ReservarEstoqueCommand(ordem.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _pecaRepositoryMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
