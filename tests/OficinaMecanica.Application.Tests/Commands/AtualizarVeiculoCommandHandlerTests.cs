using FluentAssertions;
using OficinaMecanica.Application.Commands.AtualizarVeiculo;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class AtualizarVeiculoCommandHandlerTests
{
    private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AtualizarVeiculoCommandHandler _handler;

    public AtualizarVeiculoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new AtualizarVeiculoCommandHandler(_veiculoRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_VeiculoExistente_DeveAtualizarESalvar()
    {
        var veiculo = new VeiculoBuilder().ComMarca("Toyota").ComModelo("Corolla").ComAno(2020).Build();
        _veiculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(veiculo);

        var command = new AtualizarVeiculoCommand(Guid.NewGuid(), "Honda", "Civic", 2022);
        await _handler.Handle(command, CancellationToken.None);

        veiculo.Marca.Should().Be("Honda");
        veiculo.Modelo.Should().Be("Civic");
        veiculo.Ano.Should().Be(2022);
        _veiculoRepositoryMock.Verify(r => r.Atualizar(veiculo), Times.Once());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_VeiculoNaoEncontrado_DeveLancarExcecao()
    {
        _veiculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Veiculo?)null);

        var act = () => _handler.Handle(new AtualizarVeiculoCommand(Guid.NewGuid(), "Honda", "Civic", 2022), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
    }
}
