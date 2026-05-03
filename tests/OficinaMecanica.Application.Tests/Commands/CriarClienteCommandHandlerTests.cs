using FluentAssertions;
using OficinaMecanica.Application.Commands.CriarCliente;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Application.Tests.Commands;

public class CriarClienteCommandHandlerTests
{
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CriarClienteCommandHandler _handler;

    public CriarClienteCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new CriarClienteCommandHandler(_clienteRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ComDadosValidos_DeveCriarCliente()
    {
        var cliente = new ClienteBuilder().ComNome("João Silva").Build();
        var command = new CriarClienteCommand("João Silva", "52998224725", "joao@email.com", "11999999999");

        _clienteRepositoryMock
            .Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        _clienteRepositoryMock
            .Setup(r => r.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Nome.Should().Be("João Silva");
        result.Documento.Should().Be("529.982.247-25");
        _clienteRepositoryMock.Verify(r => r.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_ComDocumentoDuplicado_DeveLancarExcecao()
    {
        var clienteExistente = new ClienteBuilder().ComNome("Existente").Build();
        var command = new CriarClienteCommand("João", "52998224725", null, null);

        _clienteRepositoryMock
            .Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clienteExistente);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*já existe*");
    }

    [Fact]
    public async Task Handle_ComDocumentoInvalido_DeveLancarExcecao()
    {
        var command = new CriarClienteCommand("João", "00000000000", null, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*inválido*");
    }
}
