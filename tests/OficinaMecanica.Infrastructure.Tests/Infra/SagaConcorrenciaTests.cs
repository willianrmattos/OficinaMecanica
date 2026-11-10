using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// A parte mais sensivel da saga: o broker entrega em paralelo e "pelo menos uma vez". Com ConcurrencyMode.Pessimistic (bloqueio da linha
// da saga) dois eventos da mesma OS se serializam no banco, e o inbox descarta a copia do mesmo MessageId: exatamente UMA transicao,
// nada em _error e nenhum deadlock que o retry nao resolva (a vitima e repetida, nunca fica sem tratamento).
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class SagaConcorrenciaTests
{
    private const int Ordens = 12;

    private readonly InfraFixture _infra;

    public SagaConcorrenciaTests(InfraFixture infra) => _infra = infra;

    [Fact]
    public async Task DoisEventosSimultaneosDaMesmaOsEMensagemDuplicada_DevemProduzirUmaSoTransicaoCoerente()
    {
        await using var ambiente = await AmbienteDaSagaInfra.CriarAsync(_infra);
        var ordens = await Task.WhenAll(Enumerable.Range(0, Ordens).Select(_ => ambiente.CriarOrdemAsync()));
        foreach (var ordem in ordens)
            await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");

        // Para cada OS, ao mesmo tempo: a aprovacao do cliente (duas copias do MESMO MessageId) e o prazo de aprovacao vencendo.
        var disparos = new List<Task>();
        foreach (var ordem in ordens)
        {
            var prazoDeAprovacao = (await ambiente.SagaAsync(ordem.OrdemId)).PrazoAprovacaoId!.Value;
            var aprovacao = new ClienteAprovouOrcamentoBuilder().DaOrdem(ordem.OrdemId).Build();
            var prazo = new PrazoExpiradoBuilder().DaOrdem(ordem.OrdemId).DoTipo(TipoPrazo.Aprovacao).ComPrazo(prazoDeAprovacao).Build();
            disparos.Add(Task.Run(() => Task.WhenAll(
                ambiente.PublicarAsync(aprovacao), ambiente.PublicarAsync(aprovacao), ambiente.PublicarAsync(prazo))));
        }

        await Task.WhenAll(disparos);

        foreach (var ordem in ordens)
            (await Esperar.AteAsync(async () => await ambiente.EstadoDaSagaAsync(ordem.OrdemId) is "AguardandoPagamento" or "OrcamentoRecusado", TimeSpan.FromSeconds(60)))
                .Should().BeTrue($"a OS {ordem.OrdemId} deveria ter decidido (esta em '{await ambiente.EstadoDaSagaAsync(ordem.OrdemId)}')");

        // Deixa as mensagens que sobraram (a segunda copia, o evento que perdeu a corrida) serem consumidas e ignoradas.
        await Task.Delay(TimeSpan.FromSeconds(5));

        var aprovadas = 0;
        var recusadas = 0;
        foreach (var ordem in ordens)
        {
            var estado = await ambiente.EstadoDaSagaAsync(ordem.OrdemId);
            var os = await ambiente.OrdemAsync(ordem.OrdemId);
            var estoque = await ambiente.EstoqueAsync(ordem.PecaId);
            var aoBilling = (Pagamentos: ambiente.Servicos.NoBilling<SolicitarPagamento>(ordem.OrdemId), Cancelamentos: ambiente.Servicos.NoBilling<CancelarOrcamento>(ordem.OrdemId));

            if (estado == "AguardandoPagamento")
            {
                aprovadas++;
                os.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
                os.EstoqueReservado.Should().BeTrue();
                estoque.Should().Be(AmbienteDaSagaInfra.EstoqueInicial - AmbienteDaSagaInfra.QuantidadeDaPeca, "a reserva acontece uma so vez");
                aoBilling.Should().Be((1, 0), "um unico SolicitarPagamento e nenhum cancelamento do orcamento");
            }
            else
            {
                recusadas++;
                estado.Should().Be("OrcamentoRecusado");
                os.Status.Should().Be(StatusOrdemDeServico.OrcamentoRecusado);
                os.EstoqueReservado.Should().BeFalse();
                estoque.Should().Be(AmbienteDaSagaInfra.EstoqueInicial, "o prazo venceu primeiro: nada foi reservado");
                aoBilling.Should().Be((0, 1), "nenhum pagamento pedido e um unico CancelarOrcamento");
            }
        }

        (aprovadas + recusadas).Should().Be(Ordens);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);

        // Erros de banco que o MassTransit trata e que, por isso, podem aparecer no log: (1) a copia concorrente do MESMO MessageId
        // faz o INSERT do inbox bater na chave unica (e a deduplicacao funcionando); (2) uma transacao escolhida como vitima de
        // deadlock (SQL 1205) e repetida pelo retry do endpoint. Tabela pequena sob RepeatableRead (isolamento do outbox) pode
        // fazer duas OS diferentes disputarem a leitura do HistoricoStatus por varredura. Deadlock NAO tratado seria mensagem em
        // _error ou estado incoerente, e as assercoes acima ja o descartam; qualquer outra excecao reprova.
        var erros = ambiente.Infra.Logs.Registros.Where(r => r.Nivel >= LogLevel.Error).ToList();
        erros.Should().OnlyContain(
            r => r.Excecao != null
                && (r.Excecao.ToString().Contains("AK_InboxState_MessageId_ConsumerId") || r.Excecao.ToString().Contains("Error Number:1205")),
            "so a deduplicacao do inbox e uma vitima de deadlock repetida pelo retry podem logar erro");
    }

    [Fact]
    public async Task LeituraDaLinhaDaSaga_DentroDaTransacaoDoConsumer_DeveSairComUpdlockParaOBancoSerializarEventosDaMesmaOs()
    {
        var capturador = new CapturadorDeSql();
        await using var ambiente = await AmbienteDaSagaInfra.CriarAsync(
            _infra, ajustarServicos: servicos => servicos.AddDbContext<AppDbContext>(opcoes => opcoes.AddInterceptors(capturador)));
        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");
        await ambiente.AprovarAsync(ordem.OrdemId);
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoPagamento");

        // So as leituras da tabela da saga dentro de uma transacao (a do consumer): as consultas do proprio teste, que
        // acompanham o estado, rodam sem transacao e nao entram na conta.
        var leituras = capturador.Comandos
            .Where(c => c.EmTransacao && c.Sql.Contains("OrdemDeServicoSagas") && c.Sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            .ToList();

        leituras.Should().HaveCountGreaterThanOrEqualTo(5, "a saga le a propria linha a cada evento que consome (abertura, diagnostico iniciado e concluido, orcamento, aprovacao)");
        leituras.Should().OnlyContain(
            c => c.Sql.Contains("WITH (UPDLOCK, ROWLOCK)"),
            "e o UPDLOCK na leitura que faz dois eventos da mesma OS esperarem um pelo outro; o MassTransit o aplica no modo Pessimistic");
    }

    [Fact]
    public async Task MensagemDuplicadaEmParalelo_DeveSerConsumidaPelaSagaUmaSoVez()
    {
        await using var ambiente = await AmbienteDaSagaInfra.CriarAsync(_infra);
        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");
        var aprovacao = new ClienteAprovouOrcamentoBuilder().DaOrdem(ordem.OrdemId).Build();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ambiente.PublicarAsync(aprovacao)));

        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoPagamento");
        await Task.Delay(TimeSpan.FromSeconds(4));
        ambiente.Servicos.NoBilling<SolicitarPagamento>(ordem.OrdemId).Should().Be(1);
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial - AmbienteDaSagaInfra.QuantidadeDaPeca);

        var inbox = await ambiente.Infra.ConsultarAsync(db => db.Set<InboxState>().Where(i => i.MessageId == aprovacao.MessageId).ToListAsync());
        inbox.Should().ContainSingle("o inbox registra a mensagem uma vez por consumer").Which.Consumed.Should().NotBeNull();
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OrcamentoGeradoRepetidoComOMesmoMessageId_DeveManterUmSoPrazoDeAprovacao()
    {
        await using var ambiente = await AmbienteDaSagaInfra.CriarAsync(_infra);
        ambiente.Servicos.BillingResponde = false;
        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "GerandoOrcamento");
        var evento = new OrcamentoGeradoBuilder().DaOrdem(ordem.OrdemId).ComValorTotal(190m).Build();

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => ambiente.PublicarAsync(evento)));

        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");
        await Task.Delay(TimeSpan.FromSeconds(3));
        var prazos = await ambiente.Infra.ConsultarAsync(db => db.SagaPrazos.AsNoTracking().Where(p => p.CorrelationId == ordem.OrdemId).ToListAsync());
        prazos.Should().ContainSingle().Which.Tipo.Should().Be(TipoPrazo.Aprovacao);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);
    }
}
