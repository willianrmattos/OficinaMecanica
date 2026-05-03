using FluentAssertions;
using OficinaMecanica.Application.Commands.DesativarServico;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class DesativarServicoCommandHandlerTests
{
    private readonly Mock<IServicoRepository> _servicoRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DesativarServicoCommandHandler _handler;

    public DesativarServicoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new DesativarServicoCommandHandler(_servicoRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_Desativar_DeveMarcarInativo()
    {
        var servico = new ServicoBuilder().Build();
        _servicoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(servico);

        await _handler.Handle(new DesativarServicoCommand(Guid.NewGuid(), Ativar: false), CancellationToken.None);

        servico.Ativo.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_Ativar_DeveMarcarAtivo()
    {
        var servico = new ServicoBuilder().Build();
        servico.Desativar();
        _servicoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(servico);

        await _handler.Handle(new DesativarServicoCommand(Guid.NewGuid(), Ativar: true), CancellationToken.None);

        servico.Ativo.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_ServicoNaoEncontrado_DeveLancarExcecao()
    {
        _servicoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Servico?)null);

        var act = () => _handler.Handle(new DesativarServicoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
    }
}
