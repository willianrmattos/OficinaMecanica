using FluentAssertions;
using MassTransit;
using MassTransit.SagaStateMachine;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// O diagrama docs/saga-maquina-de-estados.puml e a especificacao: este teste o LE (ver DiagramaDaSaga) e o compara com o grafo que
// o MassTransit gera da maquina, entao acrescentar, remover ou redirecionar uma transicao so de um dos lados reprova. O grafo
// traz dois recortes das transicoes que movem a saga, e e neles que comparo: que eventos movem cada estado (estado -> evento) e
// para onde cada evento leva (evento -> destino). Nao comparo o trio (origem, evento, destino) porque o grafo nao liga as duas
// pontas. Ficam de fora as autotransicoes do diagrama (Origem igual a Destino): na maquina sao so passos, sem TransitionTo, e
// o comportamento delas e coberto pelos testes de cada transicao.
public class SagaGrafoDaMaquinaTests
{
    private static readonly string[] Finais = { "Concluida", "Cancelada", "OrcamentoRecusado" };

    private static IReadOnlyList<TransicaoDoDiagrama> TransicoesQueMudamDeEstado() =>
        DiagramaDaSaga.Carregar().Where(t => t.Origem != t.Destino).ToList();

    [Fact]
    public void Grafo_DeveTerExatamenteOsEventosQueMovemCadaEstadoEOsDestinosDoDiagrama()
    {
        var grafo = new OrdemDeServicoStateMachine().GetGraph();
        var transicoes = TransicoesQueMudamDeEstado();

        var estadoParaEvento = grafo.Edges
            .Where(e => e.From.VertexType == typeof(State)).Select(e => (e.From.Title, e.To.Title)).Distinct().ToArray();
        var eventoParaDestino = grafo.Edges
            .Where(e => e.From.VertexType == typeof(Event)).Select(e => (e.From.Title, e.To.Title)).Distinct().ToArray();

        estadoParaEvento.Should().BeEquivalentTo(transicoes.Select(t => (t.Origem, t.Evento)).Distinct());
        eventoParaDestino.Should().BeEquivalentTo(transicoes.Select(t => (t.Evento, t.Destino)).Distinct());
    }

    [Fact]
    public void Diagrama_DeveCitarExatamenteOsEstadosDaMaquina()
    {
        var maquina = new OrdemDeServicoStateMachine();
        var daMaquina = maquina.States.Select(s => s.Name).Where(n => n is not ("Initial" or "Final")).ToArray();

        var doDiagrama = DiagramaDaSaga.Carregar()
            .SelectMany(t => new[] { t.Origem, t.Destino }).Where(n => n != DiagramaDaSaga.EstadoInicial).Distinct().ToArray();

        daMaquina.Should().HaveCount(12, "9 estados ativos e 3 finais");
        doDiagrama.Should().BeEquivalentTo(daMaquina);
    }

    [Fact]
    public void Grafo_NaoDeveTerSaidaDosEstadosFinais()
    {
        var grafo = new OrdemDeServicoStateMachine().GetGraph();

        grafo.Edges.Where(e => e.From.VertexType == typeof(State) && Finais.Contains(e.From.Title))
            .Should().BeEmpty("os finais so ignoram eventos");
        DiagramaDaSaga.Carregar().Where(t => Finais.Contains(t.Origem))
            .Should().BeEmpty("o diagrama tambem nao tem transicao saindo de um final");
    }
}
