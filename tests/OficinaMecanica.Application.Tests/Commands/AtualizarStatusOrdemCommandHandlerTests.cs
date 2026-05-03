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

    private void ConfigurarOrdem(OrdemDeServico ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Fact]
    public async Task Handle_TransicaoParaEmDiagnostico_DeveAvancarStatus()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)StatusOrdemDeServico.EmDiagnostico), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_TransicaoParaAguardandoAprovacao_DeveAvancarStatus()
    {
        var ordem = new OrdemDeServicoBuilder().EmDiagnostico();
        ordem.AdicionarServico(Guid.NewGuid(), "Alinhamento", 80m, 1);
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)StatusOrdemDeServico.AguardandoAprovacao), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
    }

    [Fact]
    public async Task Handle_TransicaoParaFinalizada_DeveAvancarStatus()
    {
        var ordem = new OrdemDeServicoBuilder().EmExecucao();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)StatusOrdemDeServico.Finalizada), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        ordem.DataConclusao.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_TransicaoParaEntregue_DeveAvancarStatus()
    {
        var ordem = new OrdemDeServicoBuilder().Finalizada();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)StatusOrdemDeServico.Entregue), CancellationToken.None);

        ordem.Status.Should().Be(StatusOrdemDeServico.Entregue);
    }

    [Fact]
    public async Task Handle_TransicaoParaEmExecucao_DeveLancarExcecaoOrientandoEndpointDeAprovacao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        var act = () => _handler.Handle(new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)StatusOrdemDeServico.EmExecucao), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*endpoint de aprovação*");
    }

    [Fact]
    public async Task Handle_StatusInvalido_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ConfigurarOrdem(ordem);

        var act = () => _handler.Handle(new AtualizarStatusOrdemCommand(Guid.NewGuid(), 999), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*inválido*");
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var act = () => _handler.Handle(new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)StatusOrdemDeServico.EmDiagnostico), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task Handle_TransicaoInvalidaDoDominio_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ConfigurarOrdem(ordem);

        var act = () => _handler.Handle(new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)StatusOrdemDeServico.Finalizada), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
