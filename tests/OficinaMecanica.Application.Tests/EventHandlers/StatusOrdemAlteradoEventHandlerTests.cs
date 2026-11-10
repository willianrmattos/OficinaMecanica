using FluentAssertions;
using OficinaMecanica.Application.EventHandlers;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace OficinaMecanica.Application.Tests.EventHandlers;

public class StatusOrdemAlteradoEventHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<StatusOrdemAlteradoEventHandler>> _loggerMock = new();
    private readonly StatusOrdemAlteradoEventHandler _handler;

    public StatusOrdemAlteradoEventHandlerTests()
    {
        _handler = new StatusOrdemAlteradoEventHandler(
            _ordemRepositoryMock.Object, _clienteRepositoryMock.Object, _emailServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ClienteComEmail_DeveEnviarEmail()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        var cliente = new ClienteBuilder().ComEmail("cliente@teste.com").Build();

        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(ordem.ClienteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var notification = new StatusOrdemAlteradoEvent(ordem.Id, ordem.Numero, StatusOrdemDeServico.AguardandoAprovacao);

        await _handler.Handle(notification, CancellationToken.None);

        _emailServiceMock.Verify(
            e => e.EnviarAsync("cliente@teste.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento, "Aguardando pagamento")]
    [InlineData(StatusOrdemDeServico.Cancelada, "Cancelada")]
    [InlineData(StatusOrdemDeServico.EmExecucao, "Em execução")]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado, "Orçamento recusado")]
    public async Task Handle_StatusDoFluxoComPagamento_DeveDescreverOStatusNoCorpoDoEmail(StatusOrdemDeServico status, string descricaoEsperada)
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        var cliente = new ClienteBuilder().ComEmail("cliente@teste.com").Build();
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(ordem.ClienteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        string? corpo = null;
        _emailServiceMock
            .Setup(e => e.EnviarAsync("cliente@teste.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, c, _) => corpo = c)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new StatusOrdemAlteradoEvent(ordem.Id, ordem.Numero, status), CancellationToken.None);

        corpo.Should().Contain($"status atualizado para: {descricaoEsperada}");
    }

    [Fact]
    public async Task Handle_AguardandoPagamento_DeveAvisarQueOLinkChegaraEmSeguida()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var cliente = new ClienteBuilder().ComEmail("cliente@teste.com").Build();
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(ordem.ClienteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        string? corpo = null;
        _emailServiceMock
            .Setup(e => e.EnviarAsync("cliente@teste.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, c, _) => corpo = c)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new StatusOrdemAlteradoEvent(ordem.Id, ordem.Numero, StatusOrdemDeServico.AguardandoPagamento), CancellationToken.None);

        corpo.Should().Contain("o link de pagamento chegará em seguida");
    }

    [Fact]
    public async Task Handle_ClienteSemEmail_NaoDeveEnviarEmail()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        var cliente = new ClienteBuilder().SemEmail().Build();

        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(ordem.ClienteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var notification = new StatusOrdemAlteradoEvent(ordem.Id, ordem.Numero, StatusOrdemDeServico.AguardandoAprovacao);

        await _handler.Handle(notification, CancellationToken.None);

        _emailServiceMock.Verify(
            e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_NaoDeveLancarNemEnviarEmail()
    {
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var notification = new StatusOrdemAlteradoEvent(Guid.NewGuid(), "OS-20260101-ABCDEF", StatusOrdemDeServico.EmDiagnostico);

        var act = async () => await _handler.Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _emailServiceMock.Verify(
            e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }
}
