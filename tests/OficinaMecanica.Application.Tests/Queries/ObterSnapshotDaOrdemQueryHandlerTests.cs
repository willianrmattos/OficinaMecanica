using FluentAssertions;
using OficinaMecanica.Application.Queries.ObterSnapshotDaOrdem;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests.Queries;

public class ObterSnapshotDaOrdemQueryHandlerTests
{
    private readonly Mock<IOrdemDeServicoRepository> _ordemRepositoryMock = new();
    private readonly ObterSnapshotDaOrdemQueryHandler _handler;

    public ObterSnapshotDaOrdemQueryHandlerTests()
    {
        _handler = new ObterSnapshotDaOrdemQueryHandler(_ordemRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_OrdemComServicosEPecas_DeveDevolverONumeroOValorEOsItensNoFormatoDasMensagens()
    {
        var pecaId = Guid.NewGuid();
        var ordem = new OrdemDeServicoBuilder().ComPeca(pecaId, "Filtro de óleo", 40m, 3).AguardandoAprovacao("Troca de óleo", 150m);
        _ordemRepositoryMock.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);

        var snapshot = await _handler.Handle(new ObterSnapshotDaOrdemQuery(ordem.Id), CancellationToken.None);

        snapshot.Numero.Should().Be(ordem.Numero);
        snapshot.ValorTotal.Should().Be(270m);
        var servico = ordem.ItensServico.Single();
        snapshot.Servicos.Should().Equal(new ItemDeServicoMensagem(servico.ServicoId, "Troca de óleo", 150m, 1));
        snapshot.Pecas.Should().Equal(new ItemDePecaMensagem(pecaId, "Filtro de óleo", 40m, 3));
    }

    [Fact]
    public async Task Handle_OrdemSemPecas_DeveDevolverListaDePecasVazia()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        _ordemRepositoryMock.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);

        var snapshot = await _handler.Handle(new ObterSnapshotDaOrdemQuery(ordem.Id), CancellationToken.None);

        snapshot.Pecas.Should().BeEmpty();
        snapshot.Servicos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_OrdemNaoEncontrada_DeveLancarDomainException()
    {
        _ordemRepositoryMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OficinaMecanica.Domain.Entities.OrdemDeServico?)null);

        var act = () => _handler.Handle(new ObterSnapshotDaOrdemQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }
}
