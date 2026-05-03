using FluentAssertions;
using OficinaMecanica.Application.Commands.AtualizarServico;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class AtualizarServicoCommandHandlerTests
{
    private readonly Mock<IServicoRepository> _servicoRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AtualizarServicoCommandHandler _handler;

    public AtualizarServicoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new AtualizarServicoCommandHandler(_servicoRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ServicoExistente_DeveAtualizarESalvar()
    {
        var servico = new ServicoBuilder().ComNome("Troca de Óleo").ComPreco(80m).ComTempo(30).Build();
        _servicoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(servico);

        var command = new AtualizarServicoCommand(Guid.NewGuid(), "Alinhamento", "Serviço de alinhamento", 120m, 60);
        await _handler.Handle(command, CancellationToken.None);

        servico.Nome.Should().Be("Alinhamento");
        servico.Preco.Should().Be(120m);
        servico.TempoEstimadoMinutos.Should().Be(60);
        _servicoRepositoryMock.Verify(r => r.Atualizar(servico), Times.Once());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_ServicoNaoEncontrado_DeveLancarExcecao()
    {
        _servicoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Servico?)null);

        var act = () => _handler.Handle(new AtualizarServicoCommand(Guid.NewGuid(), "Nome", null, 100m, 30), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
    }
}
