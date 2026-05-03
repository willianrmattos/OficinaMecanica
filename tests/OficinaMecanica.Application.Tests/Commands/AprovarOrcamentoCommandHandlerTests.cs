using FluentAssertions;
using OficinaMecanica.Application.Commands.AprovarOrcamento;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class AprovarOrcamentoCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock = new();
    private readonly Mock<IPecaRepository> _pecaRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AprovarOrcamentoCommandHandler _handler;

    public AprovarOrcamentoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        _veiculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Veiculo?)null);
        _handler = new AprovarOrcamentoCommandHandler(
            _ordemRepositoryMock.Object, _clienteRepositoryMock.Object, _veiculoRepositoryMock.Object,
            _pecaRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_OrdemSemPecas_DeveAprovarESalvar()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);

        var result = await _handler.Handle(new AprovarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("EmExecucao");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_OrdemComPecas_DeveRemoverEstoqueDeCadaPeca()
    {
        var pecaId = Guid.NewGuid();
        var peca = new PecaBuilder().ComEstoque(10).Build();
        var ordem = new OrdemDeServicoBuilder().EmDiagnostico();
        ordem.AdicionarServico(Guid.NewGuid(), "Troca de Óleo", 100m);
        ordem.AdicionarPeca(pecaId, "Filtro", 50m, 3);
        ordem.EnviarParaAprovacao();

        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(pecaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(peca);

        await _handler.Handle(new AprovarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        peca.QuantidadeEstoque.Should().Be(7);
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarExcecao()
    {
        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var act = () => _handler.Handle(new AprovarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task Handle_PecaNaoEncontradaNoSistema_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().EmDiagnostico();
        ordem.AdicionarServico(Guid.NewGuid(), "Troca de Óleo", 100m);
        ordem.AdicionarPeca(Guid.NewGuid(), "Filtro Inexistente", 50m, 1);
        ordem.EnviarParaAprovacao();

        _ordemRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordem);
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);

        var act = () => _handler.Handle(new AprovarOrcamentoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada no sistema*");
    }
}
