using FluentAssertions;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.EventHandlers;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.EventHandlers;

public class PagamentoSolicitadoEventHandlerTests
{
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<PagamentoSolicitadoEventHandler>> _loggerMock = new();
    private readonly PagamentoSolicitadoEventHandler _handler;

    public PagamentoSolicitadoEventHandlerTests()
    {
        _handler = new PagamentoSolicitadoEventHandler(
            _clienteRepositoryMock.Object, _emailServiceMock.Object, _loggerMock.Object);
    }

    private static PagamentoSolicitadoEvent Evento(Guid clienteId, DateTimeOffset? expiraEm = null) =>
        new(Guid.NewGuid(), "OS-20260310-ABC123", clienteId, "https://pagamento.exemplo/checkout/777",
            expiraEm ?? new DateTimeOffset(2026, 3, 12, 15, 30, 0, TimeSpan.Zero));

    [Fact]
    public async Task Handle_ClienteComEmail_DeveEnviarLinkEPrazoEmHorarioDeBrasilia()
    {
        var cliente = new ClienteBuilder().ComNome("Maria Souza").ComEmail("maria@teste.com").Build();
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        string? assunto = null;
        string? corpo = null;
        _emailServiceMock
            .Setup(e => e.EnviarAsync("maria@teste.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, a, c, _) => { assunto = a; corpo = c; })
            .Returns(Task.CompletedTask);

        await _handler.Handle(Evento(cliente.Id), CancellationToken.None);

        assunto.Should().Contain("OS-20260310-ABC123").And.Contain("link de pagamento");
        corpo.Should().Contain("Maria Souza");
        corpo.Should().Contain("https://pagamento.exemplo/checkout/777");
        corpo.Should().Contain("12/03/2026 às 12:30 (horário de Brasília)");
        _emailServiceMock.Verify(
            e => e.EnviarAsync("maria@teste.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Handle_PrazoNaMadrugadaEmUtc_DeveMostrarODiaAnteriorEmBrasilia()
    {
        var cliente = new ClienteBuilder().ComEmail("maria@teste.com").Build();
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        string? corpo = null;
        _emailServiceMock
            .Setup(e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, c, _) => corpo = c)
            .Returns(Task.CompletedTask);

        await _handler.Handle(Evento(cliente.Id, new DateTimeOffset(2026, 3, 13, 1, 5, 0, TimeSpan.Zero)), CancellationToken.None);

        corpo.Should().Contain("12/03/2026 às 22:05");
    }

    [Fact]
    public async Task Handle_ClienteSemEmail_NaoDeveEnviarEmail()
    {
        var cliente = new ClienteBuilder().SemEmail().Build();
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        await _handler.Handle(Evento(cliente.Id), CancellationToken.None);

        _emailServiceMock.Verify(
            e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Handle_ClienteNaoEncontrado_NaoDeveLancarNemEnviarEmail()
    {
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var act = async () => await _handler.Handle(Evento(Guid.NewGuid()), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _emailServiceMock.Verify(
            e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }
}
