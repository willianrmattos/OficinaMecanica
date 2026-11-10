using FluentAssertions;
using OficinaMecanica.Application.Commands.AprovarOrcamento;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Tests.Common.Fakes;

namespace OficinaMecanica.Application.Tests.Commands;

public class AprovarOrcamentoCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IIntegrationEventPublisher> _publisherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RelogioFalso _relogio = new();
    private readonly AprovarOrcamentoCommandHandler _handler;

    public AprovarOrcamentoCommandHandlerTests()
    {
        _handler = new AprovarOrcamentoCommandHandler(
            _ordemRepositoryMock.Object, _publisherMock.Object, _unitOfWorkMock.Object, _relogio);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Fact]
    public async Task Handle_OrdemAguardandoAprovacao_DevePublicarADecisaoSemMudarOStatus()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);
        ClienteAprovouOrcamento? publicada = null;
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<ClienteAprovouOrcamento>(), It.IsAny<CancellationToken>()))
            .Callback<ClienteAprovouOrcamento, CancellationToken>((m, _) => publicada = m)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(new AprovarOrcamentoCommand(ordem.Id), CancellationToken.None);

        publicada.Should().NotBeNull();
        publicada!.OrdemDeServicoId.Should().Be(ordem.Id);
        publicada.MessageId.Should().NotBe(Guid.Empty);
        publicada.OcorridoEm.Should().Be(_relogio.GetUtcNow());
        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        result.OrdemDeServicoId.Should().Be(ordem.Id);
        result.Numero.Should().Be(ordem.Numero);
        result.Status.Should().Be("AguardandoAprovacao");
    }

    [Fact]
    public async Task Handle_DevePublicarAntesDeSalvarParaOOutboxGravarAMensagem()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);
        var chamadas = new List<string>();
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<ClienteAprovouOrcamento>(), It.IsAny<CancellationToken>()))
            .Callback<ClienteAprovouOrcamento, CancellationToken>((_, _) => chamadas.Add("publicar"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(_ => chamadas.Add("salvar"))
            .ReturnsAsync(1);

        await _handler.Handle(new AprovarOrcamentoCommand(ordem.Id), CancellationToken.None);

        chamadas.Should().Equal("publicar", "salvar");
    }

    [Fact]
    public async Task Handle_ChamadasRepetidas_DevemGerarMessageIdsDistintos()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        ConfigurarOrdem(ordem);
        var ids = new List<Guid>();
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<ClienteAprovouOrcamento>(), It.IsAny<CancellationToken>()))
            .Callback<ClienteAprovouOrcamento, CancellationToken>((m, _) => ids.Add(m.MessageId))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new AprovarOrcamentoCommand(ordem.Id), CancellationToken.None);
        await _handler.Handle(new AprovarOrcamentoCommand(ordem.Id), CancellationToken.None);

        ids.Should().HaveCount(2).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Handle_OrdemAnteriorASaga_DeveLancarConflitoENaoPublicar()
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().AnteriorASaga().AguardandoAprovacao());

        var act = () => _handler.Handle(new AprovarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

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

        var act = () => _handler.Handle(new AprovarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ConflitoException>().WithMessage($"*AguardandoAprovacao*{status}*");
        NadaFoiPublicadoNemSalvo();
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecaoENaoPublicar()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new AprovarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
        NadaFoiPublicadoNemSalvo();
    }

    [Fact]
    public async Task Handle_FalhaAoPublicar_NaoDeveSalvar()
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().AguardandoAprovacao());
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<ClienteAprovouOrcamento>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker indisponível"));

        var act = () => _handler.Handle(new AprovarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    private void NadaFoiPublicadoNemSalvo()
    {
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<ClienteAprovouOrcamento>(), It.IsAny<CancellationToken>()), Times.Never());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }
}
