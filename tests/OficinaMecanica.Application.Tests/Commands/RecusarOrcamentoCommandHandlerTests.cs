using FluentAssertions;
using OficinaMecanica.Application.Commands.RecusarOrcamento;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Tests.Common.Fakes;

namespace OficinaMecanica.Application.Tests.Commands;

public class RecusarOrcamentoCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IIntegrationEventPublisher> _publisherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RelogioFalso _relogio = new();
    private readonly RecusarOrcamentoCommandHandler _handler;

    public RecusarOrcamentoCommandHandlerTests()
    {
        _handler = new RecusarOrcamentoCommandHandler(
            _ordemRepositoryMock.Object, _publisherMock.Object, _unitOfWorkMock.Object, _relogio);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Fact]
    public async Task Handle_OrdemAguardandoAprovacao_DevePublicarARecusaComMotivoSemMudarOStatus()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);
        ClienteRecusouOrcamento? publicada = null;
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<ClienteRecusouOrcamento>(), It.IsAny<CancellationToken>()))
            .Callback<ClienteRecusouOrcamento, CancellationToken>((m, _) => publicada = m)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(new RecusarOrcamentoCommand(ordem.Id, "Achei caro."), CancellationToken.None);

        publicada.Should().NotBeNull();
        publicada!.OrdemDeServicoId.Should().Be(ordem.Id);
        publicada.Motivo.Should().Be("Achei caro.");
        publicada.MessageId.Should().NotBe(Guid.Empty);
        publicada.OcorridoEm.Should().Be(_relogio.GetUtcNow());
        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        result.Status.Should().Be("AguardandoAprovacao");
        result.OrdemDeServicoId.Should().Be(ordem.Id);
    }

    [Fact]
    public async Task Handle_SemMotivo_DevePublicarComMotivoNulo()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);

        await _handler.Handle(new RecusarOrcamentoCommand(ordem.Id), CancellationToken.None);

        _publisherMock.Verify(
            p => p.PublishAsync(It.Is<ClienteRecusouOrcamento>(m => m.Motivo == null), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Handle_DevePublicarAntesDeSalvarParaOOutboxGravarAMensagem()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);
        var chamadas = new List<string>();
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<ClienteRecusouOrcamento>(), It.IsAny<CancellationToken>()))
            .Callback<ClienteRecusouOrcamento, CancellationToken>((_, _) => chamadas.Add("publicar"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(_ => chamadas.Add("salvar"))
            .ReturnsAsync(1);

        await _handler.Handle(new RecusarOrcamentoCommand(ordem.Id), CancellationToken.None);

        chamadas.Should().Equal("publicar", "salvar");
    }

    [Fact]
    public async Task Handle_OrdemAnteriorASaga_DeveLancarConflitoENaoPublicar()
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().AnteriorASaga().AguardandoAprovacao());

        var act = () => _handler.Handle(new RecusarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ConflitoException>().WithMessage("*antes do fluxo*");
        NadaFoiPublicadoNemSalvo();
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public async Task Handle_OrdemForaDeAguardandoAprovacao_DeveLancarConflitoENaoPublicar(StatusOrdemDeServico status)
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().NoStatus(status));

        var act = () => _handler.Handle(new RecusarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ConflitoException>().WithMessage($"*AguardandoAprovacao*{status}*");
        NadaFoiPublicadoNemSalvo();
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecaoENaoPublicar()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new RecusarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
        NadaFoiPublicadoNemSalvo();
    }

    private void NadaFoiPublicadoNemSalvo()
    {
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<ClienteRecusouOrcamento>(), It.IsAny<CancellationToken>()), Times.Never());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }
}
