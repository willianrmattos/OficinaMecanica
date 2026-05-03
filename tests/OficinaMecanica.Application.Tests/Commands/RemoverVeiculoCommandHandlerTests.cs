using FluentAssertions;
using OficinaMecanica.Application.Commands.RemoverVeiculo;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class RemoverVeiculoCommandHandlerTests
{
    private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RemoverVeiculoCommandHandler _handler;

    public RemoverVeiculoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new RemoverVeiculoCommandHandler(_veiculoRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_VeiculoExistente_DeveRemoverESalvar()
    {
        var veiculo = new VeiculoBuilder().Build();
        var veiculoId = veiculo.Id;

        _veiculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(veiculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(veiculo);

        await _handler.Handle(new RemoverVeiculoCommand(veiculoId), CancellationToken.None);

        _veiculoRepositoryMock.Verify(r => r.Remover(veiculo), Times.Once());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_VeiculoNaoEncontrado_DeveLancarExcecao()
    {
        _veiculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Veiculo?)null);

        var act = () => _handler.Handle(new RemoverVeiculoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
    }
}
