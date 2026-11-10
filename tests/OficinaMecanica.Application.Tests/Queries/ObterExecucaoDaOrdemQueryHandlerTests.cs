using FluentAssertions;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Application.Queries.ObterExecucaoDaOrdem;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Queries;

public class ObterExecucaoDaOrdemQueryHandlerTests
{
    private static readonly DateTimeOffset AtualizadaEm = new(2026, 3, 10, 15, 30, 0, TimeSpan.Zero);

    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<ISagaStateReader> _sagaMock = new();
    private readonly Mock<IExecucaoClient> _execucaoMock = new();
    private readonly Mock<ILogger<ObterExecucaoDaOrdemQueryHandler>> _loggerMock = new();
    private readonly ObterExecucaoDaOrdemQueryHandler _handler;

    public ObterExecucaoDaOrdemQueryHandlerTests()
    {
        _loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _handler = new ObterExecucaoDaOrdemQueryHandler(_ordemRepositoryMock.Object, _sagaMock.Object, _execucaoMock.Object, _loggerMock.Object);
    }

    private OrdemDeServico OrdemExistente()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        _ordemRepositoryMock.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        _sagaMock.Setup(s => s.ObterAsync(ordem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstadoConhecidoDaSaga("AguardandoPagamento", AtualizadaEm));
        return ordem;
    }

    private static ProducaoDaOrdemDto Producao() => new(
        "EmExecucao",
        "mecanico-7",
        new LaudoDaProducaoDto("Pastilhas gastas.", 2.5m),
        null,
        null,
        null,
        AtualizadaEm.AddDays(-1),
        AtualizadaEm.AddHours(-2),
        new[] { new EtapaDaProducaoDto("Execucao", "mecanico-7", "EmAndamento", AtualizadaEm.AddHours(-2), null) },
        new[] { new ApontamentoDaProducaoDto("mecanico-7", "Peça trocada.", AtualizadaEm.AddHours(-1)) });

    private void VerificarLog(LogLevel nivel, Times vezes) =>
        _loggerMock.Verify(
            l => l.Log(nivel, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            vezes);

    [Fact]
    public async Task Handle_ExecucaoResponde_DeveDevolverODetalheDaProducaoComOrigemExecucaoEOEstadoDaSaga()
    {
        var ordem = OrdemExistente();
        var producao = Producao();
        _execucaoMock.Setup(e => e.ObterOrdemDeProducaoAsync(ordem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoDaConsultaAExecucao.Encontrada(producao));

        var resposta = await _handler.Handle(new ObterExecucaoDaOrdemQuery(ordem.Id), CancellationToken.None);

        resposta.Should().NotBeNull();
        resposta!.Origem.Should().Be("execucao");
        resposta.Producao.Should().BeSameAs(producao);
        resposta.MotivoDoFallback.Should().BeNull();
        resposta.OrdemDeServicoId.Should().Be(ordem.Id);
        resposta.Numero.Should().Be(ordem.Numero);
        resposta.StatusDaOrdem.Should().Be("AguardandoPagamento");
        resposta.EstadoDaSaga.Should().Be("AguardandoPagamento");
        resposta.EstadoDaSagaAtualizadoEm.Should().Be(AtualizadaEm);
        VerificarLog(LogLevel.Warning, Times.Never());
    }

    [Fact]
    public async Task Handle_Execucao404_DeveDevolverOEstadoDaSagaSemInventarDadosDaProducaoESemAlerta()
    {
        var ordem = OrdemExistente();
        _execucaoMock.Setup(e => e.ObterOrdemDeProducaoAsync(ordem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoDaConsultaAExecucao.NaoEncontrada());

        var resposta = await _handler.Handle(new ObterExecucaoDaOrdemQuery(ordem.Id), CancellationToken.None);

        resposta!.Origem.Should().Be("saga");
        resposta.MotivoDoFallback.Should().Be("ProducaoNaoEnfileirada");
        resposta.Producao.Should().BeNull("a saga nao tem dados da producao e nada pode ser inventado");
        resposta.EstadoDaSaga.Should().Be("AguardandoPagamento");
        resposta.EstadoDaSagaAtualizadoEm.Should().Be(AtualizadaEm);
        resposta.StatusDaOrdem.Should().Be("AguardandoPagamento");
        VerificarLog(LogLevel.Warning, Times.Never());
        VerificarLog(LogLevel.Debug, Times.Once());
    }

    [Theory]
    [InlineData("HTTP 503")]
    [InlineData("circuito aberto")]
    [InlineData("timeout")]
    public async Task Handle_ExecucaoIndisponivel_DeveDevolverOEstadoDaSagaEAvisarNoLog(string detalhe)
    {
        var ordem = OrdemExistente();
        _execucaoMock.Setup(e => e.ObterOrdemDeProducaoAsync(ordem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoDaConsultaAExecucao.Indisponivel(detalhe));

        var resposta = await _handler.Handle(new ObterExecucaoDaOrdemQuery(ordem.Id), CancellationToken.None);

        resposta!.Origem.Should().Be("saga");
        resposta.MotivoDoFallback.Should().Be("ExecucaoIndisponivel");
        resposta.Producao.Should().BeNull();
        resposta.EstadoDaSaga.Should().Be("AguardandoPagamento");
        VerificarLog(LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task Handle_OrdemAnteriorASaga_DeveDevolverEstadoDaSagaNuloEMesmoAssimResponder()
    {
        var ordem = new OrdemDeServicoBuilder().AnteriorASaga().Recebida();
        _ordemRepositoryMock.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        _sagaMock.Setup(s => s.ObterAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync((EstadoConhecidoDaSaga?)null);
        _execucaoMock.Setup(e => e.ObterOrdemDeProducaoAsync(ordem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoDaConsultaAExecucao.NaoEncontrada());

        var resposta = await _handler.Handle(new ObterExecucaoDaOrdemQuery(ordem.Id), CancellationToken.None);

        resposta!.Origem.Should().Be("saga");
        resposta.EstadoDaSaga.Should().BeNull();
        resposta.EstadoDaSagaAtualizadoEm.Should().BeNull();
        resposta.StatusDaOrdem.Should().Be("Recebida");
    }

    [Fact]
    public async Task Handle_OrdemNaoExiste_DeveDevolverNuloSemConsultarASagaNemAExecucao()
    {
        _ordemRepositoryMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrdemDeServico?)null);

        var resposta = await _handler.Handle(new ObterExecucaoDaOrdemQuery(Guid.NewGuid()), CancellationToken.None);

        resposta.Should().BeNull();
        _sagaMock.VerifyNoOtherCalls();
        _execucaoMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_DeveRepassarOCancelamentoAoRepositorioASagaEAExecucao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        using var origem = new CancellationTokenSource();
        var token = origem.Token;
        _ordemRepositoryMock.Setup(r => r.ObterPorIdAsync(ordem.Id, token)).ReturnsAsync(ordem);
        _sagaMock.Setup(s => s.ObterAsync(ordem.Id, token)).ReturnsAsync(new EstadoConhecidoDaSaga("EmExecucao", AtualizadaEm));
        _execucaoMock.Setup(e => e.ObterOrdemDeProducaoAsync(ordem.Id, token)).ReturnsAsync(ResultadoDaConsultaAExecucao.Encontrada(Producao()));

        var resposta = await _handler.Handle(new ObterExecucaoDaOrdemQuery(ordem.Id), token);

        resposta!.Origem.Should().Be("execucao");
        _execucaoMock.Verify(e => e.ObterOrdemDeProducaoAsync(ordem.Id, token), Times.Once);
    }

    [Fact]
    public async Task Handle_CancelamentoDoChamador_DevePropagarSemVirarFallback()
    {
        var ordem = OrdemExistente();
        _execucaoMock.Setup(e => e.ObterOrdemDeProducaoAsync(ordem.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        var act = () => _handler.Handle(new ObterExecucaoDaOrdemQuery(ordem.Id), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
