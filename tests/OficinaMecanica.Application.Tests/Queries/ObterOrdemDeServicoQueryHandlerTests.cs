using FluentAssertions;
using OficinaMecanica.Application.Queries.ObterOrdemDeServico;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Queries;

public class ObterOrdemDeServicoQueryHandlerTests
{
    private static readonly DateTime Inicio = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock = new();

    // O repositorio entrega a OS com o historico na ordem em que o banco o devolveu, que nao e a do tempo.
    private OrdemDeServico OrdemComHistoricoEmOrdemInversa()
    {
        var ordem = new OrdemDeServicoBuilder().Finalizada().ComHistoricoEmOrdemInversa(Inicio);

        _clienteRepositoryMock.Setup(r => r.ObterPorIdAsync(ordem.ClienteId, It.IsAny<CancellationToken>())).ReturnsAsync(new ClienteBuilder().Build());
        _veiculoRepositoryMock.Setup(r => r.ObterPorIdAsync(ordem.VeiculoId, It.IsAny<CancellationToken>())).ReturnsAsync(new VeiculoBuilder().Build());
        return ordem;
    }

    private static readonly string[] FluxoEsperado =
        { "Recebida", "EmDiagnostico", "AguardandoAprovacao", "AguardandoPagamento", "EmExecucao", "Finalizada" };

    [Fact]
    public async Task PorId_HistoricoForaDeOrdem_DeveDevolverODtoEmOrdemCronologica()
    {
        var ordem = OrdemComHistoricoEmOrdemInversa();
        _ordemRepositoryMock.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var handler = new ObterOrdemDeServicoPorIdQueryHandler(_ordemRepositoryMock.Object, _clienteRepositoryMock.Object, _veiculoRepositoryMock.Object);

        var dto = await handler.Handle(new ObterOrdemDeServicoPorIdQuery(ordem.Id), CancellationToken.None);

        dto!.HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoEsperado);
        dto.HistoricoStatus.Select(h => h.DataAlteracao).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task PorNumero_HistoricoForaDeOrdem_DeveDevolverODtoEmOrdemCronologica()
    {
        var ordem = OrdemComHistoricoEmOrdemInversa();
        _ordemRepositoryMock.Setup(r => r.ObterPorNumeroAsync(ordem.Numero, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var handler = new ObterOrdemDeServicoPorNumeroQueryHandler(_ordemRepositoryMock.Object, _clienteRepositoryMock.Object, _veiculoRepositoryMock.Object);

        var dto = await handler.Handle(new ObterOrdemDeServicoPorNumeroQuery(ordem.Numero), CancellationToken.None);

        dto!.HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoEsperado);
    }

    [Fact]
    public async Task PorId_OrdemInexistente_DeveDevolverNulo()
    {
        _ordemRepositoryMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrdemDeServico?)null);
        var handler = new ObterOrdemDeServicoPorIdQueryHandler(_ordemRepositoryMock.Object, _clienteRepositoryMock.Object, _veiculoRepositoryMock.Object);

        var dto = await handler.Handle(new ObterOrdemDeServicoPorIdQuery(Guid.NewGuid()), CancellationToken.None);

        dto.Should().BeNull();
    }
}
