using FluentAssertions;
using OficinaMecanica.Application.Commands.CriarOrdemDeServico;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Commands;

public class CriarOrdemDeServicoCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock = new();
    private readonly Mock<IServicoRepository> _servicoRepositoryMock = new();
    private readonly Mock<IPecaRepository> _pecaRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CriarOrdemDeServicoCommandHandler _handler;

    public CriarOrdemDeServicoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _ordemRepositoryMock
            .Setup(r => r.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new CriarOrdemDeServicoCommandHandler(
            _ordemRepositoryMock.Object, _clienteRepositoryMock.Object, _veiculoRepositoryMock.Object,
            _servicoRepositoryMock.Object, _pecaRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ComDadosValidos_DeveCriarOrdem()
    {
        var cliente = new ClienteBuilder().Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();
        var servico = new ServicoBuilder().ComPreco(150m).Build();

        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        _veiculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(veiculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(veiculo);
        _servicoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(servico);

        var command = new CriarOrdemDeServicoCommand(
            cliente.Id, veiculo.Id, "Teste",
            new List<ItemServicoInput> { new(servico.Id, 1) },
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("Recebida");
        result.ValorTotal.Should().Be(servico.Preco);
        _ordemRepositoryMock.Verify(r => r.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_ClienteNaoEncontrado_DeveLancarExcecao()
    {
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var command = new CriarOrdemDeServicoCommand(
            Guid.NewGuid(), Guid.NewGuid(), null,
            new List<ItemServicoInput> { new(Guid.NewGuid(), 1) },
            null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Cliente não encontrado*");
    }

    [Fact]
    public async Task Handle_VeiculoNaoPertenceAoCliente_DeveLancarExcecao()
    {
        var cliente = new ClienteBuilder().Build();
        var veiculoDeOutroCliente = new VeiculoBuilder().DoCliente(Guid.NewGuid()).Build();

        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        _veiculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(veiculoDeOutroCliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(veiculoDeOutroCliente);

        var command = new CriarOrdemDeServicoCommand(
            cliente.Id, veiculoDeOutroCliente.Id, null,
            new List<ItemServicoInput> { new(Guid.NewGuid(), 1) },
            null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não pertence*");
    }
}
