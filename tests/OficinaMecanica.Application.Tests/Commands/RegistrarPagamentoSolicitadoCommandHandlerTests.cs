using FluentAssertions;
using OficinaMecanica.Application.Commands.RegistrarPagamentoSolicitado;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class RegistrarPagamentoSolicitadoCommandHandlerTests
{
    private static readonly DateTimeOffset Prazo = new(2026, 3, 12, 15, 30, 0, TimeSpan.Zero);

    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RegistrarPagamentoSolicitadoCommandHandler _handler;

    public RegistrarPagamentoSolicitadoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new RegistrarPagamentoSolicitadoCommandHandler(_ordemRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Fact]
    public async Task Handle_OrdemAguardandoPagamento_DeveGravarLinkEPrazoESalvar()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var pagamentoId = Guid.NewGuid();
        ordem.ClearDomainEvents();
        ConfigurarOrdem(ordem);

        await _handler.Handle(
            new RegistrarPagamentoSolicitadoCommand(ordem.Id, pagamentoId, "https://pagamento.exemplo/checkout/1", Prazo),
            CancellationToken.None);

        ordem.PagamentoId.Should().Be(pagamentoId);
        ordem.LinkPagamento.Should().Be("https://pagamento.exemplo/checkout/1");
        ordem.PagamentoExpiraEm.Should().Be(Prazo);
        ordem.DomainEvents.OfType<PagamentoSolicitadoEvent>().Should().ContainSingle();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_MesmoPagamentoDeNovo_NaoDeveLevantarOEventoDeNovo()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var comando = new RegistrarPagamentoSolicitadoCommand(ordem.Id, Guid.NewGuid(), "https://pagamento.exemplo/checkout/1", Prazo);
        ConfigurarOrdem(ordem);
        await _handler.Handle(comando, CancellationToken.None);
        ordem.ClearDomainEvents();

        await _handler.Handle(comando, CancellationToken.None);

        ordem.DomainEvents.Should().BeEmpty();
        ordem.PagamentoId.Should().Be(comando.PagamentoId);
    }

    [Fact]
    public async Task Handle_OrdemForaDeAguardandoPagamento_DeveLancarExcecaoSemSalvar()
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().AguardandoAprovacao());

        var act = () => _handler.Handle(
            new RegistrarPagamentoSolicitadoCommand(Guid.NewGuid(), Guid.NewGuid(), "https://pagamento.exemplo/checkout/1", Prazo),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*AguardandoPagamento*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(
            new RegistrarPagamentoSolicitadoCommand(Guid.NewGuid(), Guid.NewGuid(), "https://pagamento.exemplo/checkout/1", Prazo),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
