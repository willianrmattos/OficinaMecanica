using FluentAssertions;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// O leitor do diagrama e o que da valor ao teste do grafo: se ele devolvesse pouco ou nada, a comparacao seria vazia e passaria
// sem proteger nada. Aqui ele e testado sozinho, com texto inline, e contra o arquivo de verdade.
public class DiagramaDaSagaTests
{
    [Fact]
    public void Interpretar_DeveSepararEventosPorBarraEDescartarCondicaoComentarioEPassos()
    {
        var puml = string.Join("\n",
            "@startuml",
            @"AguardandoAprovacao --> OrcamentoRecusado : ClienteRecusouOrcamento | PrazoExpirado\n/ Send CancelarOrcamento\n(OS OrcamentoRecusado)",
            @"CancelandoPagamento --> EmExecucao : PagamentoAprovado [cancelou por prazo]\n/ Send IniciarExecucao",
            @"AguardandoDiagnostico --> GerandoOrcamento : DiagnosticoConcluido (chegou antes do DiagnosticoIniciado)\n/ adianta o inicio",
            "@enduml");

        DiagramaDaSaga.Interpretar(puml).Should().Equal(
            new TransicaoDoDiagrama("AguardandoAprovacao", "ClienteRecusouOrcamento", "OrcamentoRecusado"),
            new TransicaoDoDiagrama("AguardandoAprovacao", "PrazoExpirado", "OrcamentoRecusado"),
            new TransicaoDoDiagrama("CancelandoPagamento", "PagamentoAprovado", "EmExecucao"),
            new TransicaoDoDiagrama("AguardandoDiagnostico", "DiagnosticoConcluido", "GerandoOrcamento"));
    }

    [Fact]
    public void Interpretar_DeveTraduzirFaultParaOEventoFalhaAoEOEstadoInicial()
    {
        var puml = string.Join("\r\n",
            @"[*] --> AguardandoDiagnostico : OrdemDeServicoAberta\n/ Send SolicitarDiagnostico",
            @"GerandoOrcamento --> Cancelada : OrcamentoNaoGerado | Fault<GerarOrcamento>\n(OS Cancelada)");

        DiagramaDaSaga.Interpretar(puml).Should().Equal(
            new TransicaoDoDiagrama("Initial", "OrdemDeServicoAberta", "AguardandoDiagnostico"),
            new TransicaoDoDiagrama("GerandoOrcamento", "OrcamentoNaoGerado", "Cancelada"),
            new TransicaoDoDiagrama("GerandoOrcamento", "FalhaAoGerarOrcamento", "Cancelada"));
    }

    [Fact]
    public void Interpretar_DeveIgnorarEstadosFinaisNotasETitulo()
    {
        var puml = string.Join("\n",
            "title Saga",
            "Concluida --> [*]",
            "note right of Cancelada",
            "  Fault<CancelarPagamento | CancelarOrcamento>: so log",
            "end note",
            @"EmExecucao --> Concluida : ExecucaoFinalizada\n(OS Finalizada)");

        DiagramaDaSaga.Interpretar(puml).Should().ContainSingle()
            .Which.Should().Be(new TransicaoDoDiagrama("EmExecucao", "ExecucaoFinalizada", "Concluida"));
    }

    [Fact]
    public void Interpretar_RotuloQueNaoENomeDeEvento_DeveFalharNomeandoALinha()
    {
        var puml = @"AguardandoAprovacao --> Cancelada : estoque insuficiente\n/ Send CancelarOrcamento";

        var ler = () => DiagramaDaSaga.Interpretar(puml);

        ler.Should().Throw<FormatException>().WithMessage("*'estoque insuficiente'*AguardandoAprovacao --> Cancelada*");
    }

    [Fact]
    public void Carregar_DeveLerODiagramaDoRepositorioComAsTransicoesDaSaga()
    {
        File.Exists(DiagramaDaSaga.Localizar()).Should().BeTrue();

        var transicoes = DiagramaDaSaga.Carregar();

        transicoes.Should().HaveCountGreaterThanOrEqualTo(35, "o diagrama tem 35 transicoes (evento a evento): uma leitura curta deixaria a comparacao vazia");
        transicoes.Should().Contain(new TransicaoDoDiagrama("Initial", "OrdemDeServicoAberta", "AguardandoDiagnostico"));
        transicoes.Should().Contain(new TransicaoDoDiagrama("AguardandoAprovacao", "ClienteAprovouOrcamento", "Cancelada"), "o ramo de estoque insuficiente");
        transicoes.Should().Contain(new TransicaoDoDiagrama("Estornando", "FalhaAoEstornarPagamento", "EstornoPendenteManual"));
        transicoes.Select(t => t.Evento).Should().OnlyContain(e => !e.Contains(' ') && !e.Contains('['), "todo rotulo virou um nome de evento");
    }
}
