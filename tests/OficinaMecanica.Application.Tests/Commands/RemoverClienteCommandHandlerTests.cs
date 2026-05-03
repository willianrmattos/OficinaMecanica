using FluentAssertions;
using OficinaMecanica.Application.Commands.RemoverCliente;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class RemoverClienteCommandHandlerTests
{
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RemoverClienteCommandHandler _handler;

    public RemoverClienteCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new RemoverClienteCommandHandler(
            _clienteRepositoryMock.Object, _ordemRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_SemOrdens_DeveRemoverESalvar()
    {
        var cliente = new ClienteBuilder().Build();
        var clienteId = cliente.Id;

        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(clienteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        _ordemRepositoryMock
            .Setup(r => r.ListarPorClienteAsync(clienteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<OrdemDeServico>());

        await _handler.Handle(new RemoverClienteCommand(clienteId), CancellationToken.None);

        _clienteRepositoryMock.Verify(r => r.Remover(cliente), Times.Once());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_ComOrdens_DeveLancarExcecao()
    {
        var cliente = new ClienteBuilder().Build();
        var clienteId = cliente.Id;
        var ordem = new OrdemDeServicoBuilder().DoCliente(clienteId).Recebida();

        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(clienteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        _ordemRepositoryMock
            .Setup(r => r.ListarPorClienteAsync(clienteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ordem });

        var act = () => _handler.Handle(new RemoverClienteCommand(clienteId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*ordens de serviço*");
    }

    [Fact]
    public async Task Handle_ClienteNaoEncontrado_DeveLancarExcecao()
    {
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var act = () => _handler.Handle(new RemoverClienteCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
    }
}
