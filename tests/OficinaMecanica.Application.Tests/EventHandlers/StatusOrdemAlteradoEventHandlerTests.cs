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
