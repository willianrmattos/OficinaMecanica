using FluentAssertions;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;

namespace OficinaMecanica.Domain.Tests.Entities;

// O historico chega do banco na ordem do indice da chave (Guid aleatorio): quem le a OS nao pode depender da ordem em que o EF
// preencheu a lista. Estes testes entregam as linhas ao agregado embaralhadas, como o EF as entrega.
public class OrdemDeServicoHistoricoEmOrdemTests
{
    private static readonly DateTime Inicio = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private static HistoricoStatus Linha(OrdemDeServico ordem, StatusOrdemDeServico status, int horas) =>
        HistoricoEmbaralhado.Linha(ordem.Id, status, Inicio.AddHours(horas));

    [Fact]
    public void HistoricoStatus_LinhasEmOrdemEmbaralhada_DeveDevolverEmOrdemCronologicaCrescente()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ordem.ComHistoricoNaOrdem(
            Linha(ordem, StatusOrdemDeServico.Finalizada, 5),
            Linha(ordem, StatusOrdemDeServico.Recebida, 0),
            Linha(ordem, StatusOrdemDeServico.EmExecucao, 4),
            Linha(ordem, StatusOrdemDeServico.EmDiagnostico, 1),
            Linha(ordem, StatusOrdemDeServico.AguardandoPagamento, 3),
            Linha(ordem, StatusOrdemDeServico.AguardandoAprovacao, 2));

        ordem.HistoricoStatus.Select(h => h.Status).Should().Equal(
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.EmExecucao,
            StatusOrdemDeServico.Finalizada);
        ordem.HistoricoStatus.Select(h => h.DataAlteracao).Should().BeInAscendingOrder();
    }

    [Fact]
    public void HistoricoStatus_TodasAsLinhasNoMesmoInstante_DeveDesempatarPelaPosicaoNoFluxoEnaoPeloValorDoEnum()
    {
        // AguardandoPagamento vale 7 no enum (entrou no fim para nao renumerar), mas vem antes de EmExecucao (4) no fluxo.
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ordem.ComHistoricoNaOrdem(
            Linha(ordem, StatusOrdemDeServico.Finalizada, 0),
            Linha(ordem, StatusOrdemDeServico.EmExecucao, 0),
            Linha(ordem, StatusOrdemDeServico.AguardandoPagamento, 0),
            Linha(ordem, StatusOrdemDeServico.AguardandoAprovacao, 0),
            Linha(ordem, StatusOrdemDeServico.EmDiagnostico, 0),
            Linha(ordem, StatusOrdemDeServico.Recebida, 0));

        ordem.HistoricoStatus.Select(h => h.Status).Should().Equal(
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.EmExecucao,
            StatusOrdemDeServico.Finalizada);
    }

    [Fact]
    public void HistoricoStatus_EmpateSoEntreAlgumasLinhas_DeveManterAsDemaisPelaDataEDesempatarAsEmpatadas()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ordem.ComHistoricoNaOrdem(
            Linha(ordem, StatusOrdemDeServico.EmDiagnostico, 1),
            Linha(ordem, StatusOrdemDeServico.EmExecucao, 3),
            Linha(ordem, StatusOrdemDeServico.AguardandoAprovacao, 1),
            Linha(ordem, StatusOrdemDeServico.Recebida, 0),
            Linha(ordem, StatusOrdemDeServico.AguardandoPagamento, 3));

        ordem.HistoricoStatus.Select(h => h.Status).Should().Equal(
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.EmExecucao);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado, StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.Cancelada, StatusOrdemDeServico.EmExecucao)]
    public void HistoricoStatus_StatusTerminalNoMesmoInstanteDoAnterior_DeveFicarDepoisDele(StatusOrdemDeServico terminal, StatusOrdemDeServico anterior)
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ordem.ComHistoricoNaOrdem(Linha(ordem, terminal, 2), Linha(ordem, anterior, 2), Linha(ordem, StatusOrdemDeServico.Recebida, 0));

        ordem.HistoricoStatus.Select(h => h.Status).Should().Equal(StatusOrdemDeServico.Recebida, anterior, terminal);
    }

    [Fact]
    public void HistoricoStatus_TransicoesDoDominioEmSequenciaRapida_DeveManterAOrdemEmQueOcorreram()
    {
        // Sem pausa entre as transicoes o relogio pode devolver a mesma data: a ordem nao pode depender disso.
        var ordem = new OrdemDeServicoBuilder().EmExecucao();
        ordem.Cancelar("Execucao inviavel.");

        ordem.HistoricoStatus.Select(h => h.Status).Should().Equal(
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.EmExecucao,
            StatusOrdemDeServico.Cancelada);
    }

    [Fact]
    public void HistoricoStatus_AposNovaTransicao_DeveIncluirANovaLinhaNoFim()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        var antes = ordem.HistoricoStatus;

        ordem.AvancarParaDiagnostico();

        antes.Should().ContainSingle("a leitura anterior e um retrato");
        ordem.HistoricoStatus.Select(h => h.Status).Should().Equal(StatusOrdemDeServico.Recebida, StatusOrdemDeServico.EmDiagnostico);
    }

    [Fact]
    public void PosicaoNoFluxo_DeveCrescerAoLongoDoCaminhoPrincipalEPorOsTerminaisDepoisDeTodos()
    {
        var caminho = new[]
        {
            StatusOrdemDeServico.Recebida, StatusOrdemDeServico.EmDiagnostico, StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.AguardandoPagamento, StatusOrdemDeServico.EmExecucao, StatusOrdemDeServico.Finalizada,
            StatusOrdemDeServico.Entregue
        };

        caminho.Select(OrdemDeServico.PosicaoNoFluxo).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        OrdemDeServico.PosicaoNoFluxo(StatusOrdemDeServico.OrcamentoRecusado).Should().BeGreaterThan(OrdemDeServico.PosicaoNoFluxo(StatusOrdemDeServico.Entregue));
        OrdemDeServico.PosicaoNoFluxo(StatusOrdemDeServico.Cancelada).Should().Be(OrdemDeServico.PosicaoNoFluxo(StatusOrdemDeServico.OrcamentoRecusado));
    }
}
