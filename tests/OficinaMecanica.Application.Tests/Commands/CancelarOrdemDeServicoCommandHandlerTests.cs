using FluentAssertions;
using OficinaMecanica.Application.Commands.CancelarOrdemDeServico;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Tests.Common.Fakes;

namespace OficinaMecanica.Application.Tests.Commands;

public class CancelarOrdemDeServicoCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IIntegrationEventPublisher> _publisherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RelogioFalso _relogio = new();
    private readonly CancelarOrdemDeServicoCommandHandler _handler;

    public CancelarOrdemDeServicoCommandHandlerTests()
    {
        _handler = new CancelarOrdemDeServicoCommandHandler(
            _ordemRepositoryMock.Object, _publisherMock.Object, _unitOfWorkMock.Object, _relogio);
    }

    private void ConfigurarOrdem(OrdemDeServico? ordem) =>
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    public async Task Handle_AntesDaExecucao_DevePublicarCancelamentoSolicitadoSemMudarOStatus(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        ConfigurarOrdem(ordem);
        CancelamentoSolicitado? publicada = null;
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<CancelamentoSolicitado>(), It.IsAny<CancellationToken>()))
            .Callback<CancelamentoSolicitado, CancellationToken>((m, _) => publicada = m)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(new CancelarOrdemDeServicoCommand(ordem.Id, "  Cliente desistiu.  "), CancellationToken.None);

        publicada.Should().NotBeNull();
        publicada!.OrdemDeServicoId.Should().Be(ordem.Id);
        publicada.Motivo.Should().Be("Cliente desistiu.");
        publicada.MessageId.Should().NotBe(Guid.Empty);
        publicada.OcorridoEm.Should().Be(_relogio.GetUtcNow());
        ordem.Status.Should().Be(status);
        result.Status.Should().Be(status.ToString());
        result.OrdemDeServicoId.Should().Be(ordem.Id);
    }

    [Fact]
    public async Task Handle_DevePublicarAntesDeSalvarParaOOutboxGravarAMensagem()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ConfigurarOrdem(ordem);
        var chamadas = new List<string>();
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<CancelamentoSolicitado>(), It.IsAny<CancellationToken>()))
            .Callback<CancelamentoSolicitado, CancellationToken>((_, _) => chamadas.Add("publicar"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(_ => chamadas.Add("salvar"))
            .ReturnsAsync(1);

        await _handler.Handle(new CancelarOrdemDeServicoCommand(ordem.Id, "Motivo."), CancellationToken.None);

        chamadas.Should().Equal("publicar", "salvar");
    }

    [Fact]
    public async Task Handle_OrdemAnteriorASaga_DeveLancarConflitoENaoPublicar()
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().AnteriorASaga().AguardandoAprovacao());

        var act = () => _handler.Handle(new CancelarOrdemDeServicoCommand(Guid.NewGuid(), "Motivo."), CancellationToken.None);

        await act.Should().ThrowAsync<ConflitoException>().WithMessage("*antes do fluxo*");
        NadaFoiPublicadoNemSalvo();
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Entregue)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public async Task Handle_APartirDaExecucaoOuEmStatusTerminal_DeveLancarConflitoENaoPublicar(StatusOrdemDeServico status)
    {
        ConfigurarOrdem(new OrdemDeServicoBuilder().NoStatus(status));

        var act = () => _handler.Handle(new CancelarOrdemDeServicoCommand(Guid.NewGuid(), "Motivo."), CancellationToken.None);

        await act.Should().ThrowAsync<ConflitoException>().WithMessage($"*antes de entrar em execução*{status}*");
        NadaFoiPublicadoNemSalvo();
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecaoENaoPublicar()
    {
        ConfigurarOrdem(null);

        var act = () => _handler.Handle(new CancelarOrdemDeServicoCommand(Guid.NewGuid(), "Motivo."), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
        NadaFoiPublicadoNemSalvo();
    }

    private void NadaFoiPublicadoNemSalvo()
    {
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<CancelamentoSolicitado>(), It.IsAny<CancellationToken>()), Times.Never());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }
}
