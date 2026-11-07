using FluentAssertions;
using OficinaMecanica.Application.Commands.CriarOrdemDeServico;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Tests.Common.Fakes;

namespace OficinaMecanica.Application.Tests.Commands;

public class CriarOrdemDeServicoCommandHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock = new();
    private readonly Mock<IServicoRepository> _servicoRepositoryMock = new();
    private readonly Mock<IPecaRepository> _pecaRepositoryMock = new();
    private readonly Mock<IIntegrationEventPublisher> _publisherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RelogioFalso _relogio = new();
    private readonly CriarOrdemDeServicoCommandHandler _handler;

    private readonly Cliente _cliente;
    private readonly Veiculo _veiculo;
    private OrdemDeServico? _ordemAdicionada;

    public CriarOrdemDeServicoCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _ordemRepositoryMock
            .Setup(r => r.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()))
            .Callback<OrdemDeServico, CancellationToken>((o, _) => _ordemAdicionada = o)
            .Returns(Task.CompletedTask);

        _cliente = new ClienteBuilder().Build();
        _veiculo = new VeiculoBuilder().DoCliente(_cliente.Id).ComPlaca("ABC1D23").ComMarca("Honda").ComModelo("Civic").Build();
        _clienteRepositoryMock
            .Setup(r => r.ObterPorIdAsync(_cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cliente);
        _veiculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(_veiculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_veiculo);

        _handler = new CriarOrdemDeServicoCommandHandler(
            _ordemRepositoryMock.Object, _clienteRepositoryMock.Object, _veiculoRepositoryMock.Object,
            _servicoRepositoryMock.Object, _pecaRepositoryMock.Object, _publisherMock.Object,
            _unitOfWorkMock.Object, _relogio);
    }

    private Servico ConfigurarServico(decimal preco = 150m, string nome = "Alinhamento")
    {
        var servico = new ServicoBuilder().ComNome(nome).ComPreco(preco).Build();
        _servicoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(servico);
        return servico;
    }

    private Peca ConfigurarPeca(int estoque, string nome = "Filtro de óleo", decimal preco = 45m)
    {
        var peca = new PecaBuilder().ComNome(nome).ComPreco(preco).ComEstoque(estoque).Build();
        _pecaRepositoryMock
            .Setup(r => r.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(peca);
        return peca;
    }

    private CriarOrdemDeServicoCommand Comando(Servico servico, Peca? peca = null, int quantidadePeca = 1, string? observacoes = "Teste") =>
        new(_cliente.Id, _veiculo.Id, observacoes,
            new List<ItemServicoInput> { new(servico.Id, 1) },
            peca is null ? null : new List<ItemPecaInput> { new(peca.Id, quantidadePeca) });

    [Fact]
    public async Task Handle_ComDadosValidos_DeveCriarOrdem()
    {
        var servico = ConfigurarServico(150m);

        var result = await _handler.Handle(Comando(servico), CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("Recebida");
        result.ValorTotal.Should().Be(servico.Preco);
        _ordemRepositoryMock.Verify(r => r.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Once());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        _ordemAdicionada!.ParticipaDaSaga.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DevePublicarOrdemDeServicoAbertaComORetratoDaOrdem()
    {
        var servico = ConfigurarServico(150m, "Alinhamento");
        var peca = ConfigurarPeca(estoque: 10, nome: "Filtro de óleo", preco: 45m);
        OrdemDeServicoAberta? publicada = null;
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<OrdemDeServicoAberta>(), It.IsAny<CancellationToken>()))
            .Callback<OrdemDeServicoAberta, CancellationToken>((m, _) => publicada = m)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(Comando(servico, peca, 2, "Barulho na roda"), CancellationToken.None);

        publicada.Should().NotBeNull();
        publicada!.OrdemDeServicoId.Should().Be(result.Id);
        publicada.Numero.Should().Be(result.Numero);
        publicada.MessageId.Should().NotBe(Guid.Empty);
        publicada.OcorridoEm.Should().Be(_relogio.GetUtcNow());
        publicada.ClienteId.Should().Be(_cliente.Id);
        publicada.VeiculoId.Should().Be(_veiculo.Id);
        publicada.Placa.Should().Be("ABC1D23");
        publicada.Marca.Should().Be("Honda");
        publicada.Modelo.Should().Be("Civic");
        publicada.Observacoes.Should().Be("Barulho na roda");
        publicada.ValorTotal.Should().Be(150m + 2 * 45m);
        publicada.Servicos.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { ServicoId = servico.Id, Nome = "Alinhamento", PrecoUnitario = 150m, Quantidade = 1 });
        publicada.Pecas.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { PecaId = peca.Id, Nome = "Filtro de óleo", PrecoUnitario = 45m, Quantidade = 2 });
    }

    [Fact]
    public async Task Handle_DevePublicarAntesDeSalvarParaOOutboxGravarAMensagemJuntoComAOrdem()
    {
        var servico = ConfigurarServico();
        var chamadas = new List<string>();
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<OrdemDeServicoAberta>(), It.IsAny<CancellationToken>()))
            .Callback<OrdemDeServicoAberta, CancellationToken>((_, _) => chamadas.Add("publicar"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(_ => chamadas.Add("salvar"))
            .ReturnsAsync(1);

        await _handler.Handle(Comando(servico), CancellationToken.None);

        chamadas.Should().Equal("publicar", "salvar");
    }

    [Fact]
    public async Task Handle_ComPecas_DeveValidarOEstoqueSemReservar()
    {
        var servico = ConfigurarServico();
        var peca = ConfigurarPeca(estoque: 10);

        await _handler.Handle(Comando(servico, peca, 4), CancellationToken.None);

        peca.QuantidadeEstoque.Should().Be(10);
        _ordemAdicionada!.EstoqueReservado.Should().BeFalse();
        _ordemAdicionada.ItensPeca.Should().ContainSingle().Which.Quantidade.Should().Be(4);
    }

    [Fact]
    public async Task Handle_EstoqueInsuficiente_DeveLancarExcecaoSemPublicarNemSalvar()
    {
        var servico = ConfigurarServico();
        var peca = ConfigurarPeca(estoque: 1);

        var act = () => _handler.Handle(Comando(servico, peca, 2), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Estoque insuficiente*");
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<OrdemDeServicoAberta>(), It.IsAny<CancellationToken>()), Times.Never());
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
        peca.QuantidadeEstoque.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ChamadasSeguidas_DevemGerarMessageIdsDistintos()
    {
        var servico = ConfigurarServico();
        var ids = new List<Guid>();
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<OrdemDeServicoAberta>(), It.IsAny<CancellationToken>()))
            .Callback<OrdemDeServicoAberta, CancellationToken>((m, _) => ids.Add(m.MessageId))
            .Returns(Task.CompletedTask);

        await _handler.Handle(Comando(servico), CancellationToken.None);
        await _handler.Handle(Comando(servico), CancellationToken.None);

        ids.Should().HaveCount(2).And.OnlyHaveUniqueItems();
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
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<OrdemDeServicoAberta>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_VeiculoNaoEncontrado_DeveLancarExcecao()
    {
        var command = new CriarOrdemDeServicoCommand(
            _cliente.Id, Guid.NewGuid(), null,
            new List<ItemServicoInput> { new(Guid.NewGuid(), 1) },
            null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Veículo não encontrado*");
    }

    [Fact]
    public async Task Handle_VeiculoNaoPertenceAoCliente_DeveLancarExcecao()
    {
        var veiculoDeOutroCliente = new VeiculoBuilder().DoCliente(Guid.NewGuid()).Build();
        _veiculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(veiculoDeOutroCliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(veiculoDeOutroCliente);

        var command = new CriarOrdemDeServicoCommand(
            _cliente.Id, veiculoDeOutroCliente.Id, null,
            new List<ItemServicoInput> { new(Guid.NewGuid(), 1) },
            null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não pertence*");
    }

    [Fact]
    public async Task Handle_ServicoNaoEncontrado_DeveLancarExcecao()
    {
        var command = new CriarOrdemDeServicoCommand(
            _cliente.Id, _veiculo.Id, null,
            new List<ItemServicoInput> { new(Guid.NewGuid(), 1) },
            null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Serviço com ID*não encontrado*");
    }

    [Fact]
    public async Task Handle_ServicoInativo_DeveLancarExcecao()
    {
        var servico = ConfigurarServico();
        servico.Desativar();

        var act = () => _handler.Handle(Comando(servico), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*está inativo*");
    }

    [Fact]
    public async Task Handle_PecaNaoEncontrada_DeveLancarExcecao()
    {
        var servico = ConfigurarServico();
        var command = new CriarOrdemDeServicoCommand(
            _cliente.Id, _veiculo.Id, null,
            new List<ItemServicoInput> { new(servico.Id, 1) },
            new List<ItemPecaInput> { new(Guid.NewGuid(), 1) });

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Peça com ID*não encontrada*");
    }

    [Fact]
    public async Task Handle_PecaInativa_DeveLancarExcecao()
    {
        var servico = ConfigurarServico();
        var peca = ConfigurarPeca(estoque: 10);
        peca.Desativar();

        var act = () => _handler.Handle(Comando(servico, peca), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*está inativa*");
    }

    [Fact]
    public async Task Handle_OrdemCriada_DeveComecarEmRecebida()
    {
        var servico = ConfigurarServico();

        await _handler.Handle(Comando(servico), CancellationToken.None);

        _ordemAdicionada!.Status.Should().Be(StatusOrdemDeServico.Recebida);
    }
}
