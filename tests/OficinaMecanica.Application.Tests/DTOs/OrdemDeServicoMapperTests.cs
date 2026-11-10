using FluentAssertions;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Enums;

namespace OficinaMecanica.Application.Tests.DTOs;

public class OrdemDeServicoMapperTests
{
    private static readonly DateTimeOffset Prazo = new(2026, 3, 12, 15, 30, 0, TimeSpan.Zero);
    private static readonly DateTime Inicio = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToDto_OrdemComPagamentoSolicitado_DeveExporLinkEPrazo()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        ordem.RegistrarPagamentoSolicitado(Guid.NewGuid(), "https://pagamento.exemplo/checkout/1", Prazo);

        var dto = OrdemDeServicoMapper.ToDto(ordem, "Maria", "ABC1D23");

        dto.Status.Should().Be("AguardandoPagamento");
        dto.LinkPagamento.Should().Be("https://pagamento.exemplo/checkout/1");
        dto.PagamentoExpiraEm.Should().Be(Prazo);
    }

    [Fact]
    public void ToDto_OrdemSemPagamento_DeveDeixarLinkEPrazoNulos()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();

        var dto = OrdemDeServicoMapper.ToDto(ordem, "Maria", "ABC1D23");

        dto.LinkPagamento.Should().BeNull();
        dto.PagamentoExpiraEm.Should().BeNull();
    }

    [Fact]
    public void ToDto_DeveMapearOsDadosDaOrdemEOsItens()
    {
        var pecaId = Guid.NewGuid();
        var ordem = new OrdemDeServicoBuilder().ComPeca(pecaId, "Filtro", 45m, 2).AguardandoAprovacao("Alinhamento", 80m);

        var dto = OrdemDeServicoMapper.ToDto(ordem, "Maria", "ABC1D23");

        dto.Id.Should().Be(ordem.Id);
        dto.Numero.Should().Be(ordem.Numero);
        dto.NomeCliente.Should().Be("Maria");
        dto.PlacaVeiculo.Should().Be("ABC1D23");
        dto.ValorTotal.Should().Be(170m);
        dto.ItensServico.Should().ContainSingle().Which.NomeServico.Should().Be("Alinhamento");
        dto.ItensPeca.Should().ContainSingle().Which.PecaId.Should().Be(pecaId);
        dto.HistoricoStatus.Select(h => h.Status).Should().Equal("Recebida", "EmDiagnostico", "AguardandoAprovacao");
    }

    [Fact]
    public void ToDto_HistoricoEntreguePeloBancoForaDeOrdem_DeveExporEmOrdemCronologicaCrescente()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ordem.ComHistoricoNaOrdem(
            HistoricoEmbaralhado.Linha(ordem.Id, StatusOrdemDeServico.Finalizada, Inicio.AddHours(5)),
            HistoricoEmbaralhado.Linha(ordem.Id, StatusOrdemDeServico.Recebida, Inicio),
            HistoricoEmbaralhado.Linha(ordem.Id, StatusOrdemDeServico.EmExecucao, Inicio.AddHours(4)),
            HistoricoEmbaralhado.Linha(ordem.Id, StatusOrdemDeServico.EmDiagnostico, Inicio.AddHours(1)),
            HistoricoEmbaralhado.Linha(ordem.Id, StatusOrdemDeServico.AguardandoPagamento, Inicio.AddHours(3)),
            HistoricoEmbaralhado.Linha(ordem.Id, StatusOrdemDeServico.AguardandoAprovacao, Inicio.AddHours(2)));

        var dto = OrdemDeServicoMapper.ToDto(ordem, "Maria", "ABC1D23");

        dto.HistoricoStatus.Select(h => h.Status).Should().Equal(
            "Recebida", "EmDiagnostico", "AguardandoAprovacao", "AguardandoPagamento", "EmExecucao", "Finalizada");
        dto.HistoricoStatus.Select(h => h.DataAlteracao).Should().BeInAscendingOrder();
    }

    [Fact]
    public void ToDto_HistoricoComDatasIguais_DeveDesempatarPelaPosicaoNoFluxo()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ordem.ComHistoricoNaOrdem(
            HistoricoEmbaralhado.Linha(ordem.Id, StatusOrdemDeServico.EmExecucao, Inicio),
            HistoricoEmbaralhado.Linha(ordem.Id, StatusOrdemDeServico.AguardandoPagamento, Inicio),
            HistoricoEmbaralhado.Linha(ordem.Id, StatusOrdemDeServico.Recebida, Inicio));

        var dto = OrdemDeServicoMapper.ToDto(ordem, "Maria", "ABC1D23");

        dto.HistoricoStatus.Select(h => h.Status).Should().Equal("Recebida", "AguardandoPagamento", "EmExecucao");
    }

    [Fact]
    public void ToSolicitacaoAceita_DeveTrazerOStatusAtualSemAvancarAOrdem()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();

        var dto = OrdemDeServicoMapper.ToSolicitacaoAceita(ordem);

        dto.OrdemDeServicoId.Should().Be(ordem.Id);
        dto.Numero.Should().Be(ordem.Numero);
        dto.Status.Should().Be("AguardandoAprovacao");
    }
}
