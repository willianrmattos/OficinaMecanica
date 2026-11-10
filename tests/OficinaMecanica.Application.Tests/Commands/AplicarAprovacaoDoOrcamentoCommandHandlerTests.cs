using FluentAssertions;
using OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class AplicarAprovacaoDoOrcamentoCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AplicarAprovacaoDoOrcamentoCommandHandler _handler;

    public AplicarAprovacaoDoOrcamentoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new AplicarAprovacaoDoOrcamentoCommandHandler(_ordemRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Fact]
    public async Task Handle_OrdemAguardandoAprovacao_DeveIrParaAguardandoPagamentoSemMexerNoEstoque()
    {
        var peca = new PecaBuilder().ComEstoque(10).Build();
        var ordem = new OrdemDeServicoBuilder().ComPeca(peca.Id, peca.Nome, peca.PrecoUnitario, 3).AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AplicarAprovacaoDoOrcamentoCommand(ordem.Id), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        peca.QuantidadeEstoque.Should().Be(10);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    public async Task Handle_OrdemQueJaFoiAprovada_DeveSerNoOp(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        var historicoAntes = ordem.HistoricoStatus.Count;
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AplicarAprovacaoDoOrcamentoCommand(ordem.Id), CancellationToken.None);

        ordem.Status.Should().Be(status);
        ordem.HistoricoStatus.Should().HaveCount(historicoAntes);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_ChamadoDuasVezes_DeveAprovarUmaSoVez()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AplicarAprovacaoDoOrcamentoCommand(ordem.Id), CancellationToken.None);
        await _handler.Handle(new AplicarAprovacaoDoOrcamentoCommand(ordem.Id), CancellationToken.None);

        ordem.HistoricoStatus.Count(h => h.Status == StatusOrdemDeServico.AguardandoPagamento).Should().Be(1);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public async Task Handle_OrdemQueNaoPodeSerAprovada_DeveLancarExcecaoSemSalvar(StatusOrdemDeServico status)
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().NoStatus(status));

        var act = () => _handler.Handle(new AplicarAprovacaoDoOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Não é possível*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new AplicarAprovacaoDoOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
