using FluentAssertions;
using OficinaMecanica.Application.Commands.AtualizarCliente;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class AtualizarClienteCommandHandlerTests
{
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AtualizarClienteCommandHandler _handler;

    public AtualizarClienteCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new AtualizarClienteCommandHandler(_clienteRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ClienteExistente_DeveAtualizarESalvar()
    {
        var cliente = new ClienteBuilder().ComNome("Nome Antigo").SemEmail().Build();
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var command = new AtualizarClienteCommand(Guid.NewGuid(), "Nome Novo", "novo@email.com", "11988888888");
        await _handler.Handle(command, CancellationToken.None);

        cliente.Nome.Should().Be("Nome Novo");
        cliente.Email.Should().Be("novo@email.com");
        _clienteRepositoryMock.Verify(r => r.Atualizar(cliente), Times.Once());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_ClienteNaoEncontrado_DeveLancarExcecao()
    {
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var act = () => _handler.Handle(new AtualizarClienteCommand(Guid.NewGuid(), "Nome", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
    }
}
