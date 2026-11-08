using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// As metricas de transicao saem ANTES do commit da transacao do consumer (ver ObservadorDeMetricasDaSaga e o ADR 0008): se o commit
// falha e o retry do endpoint reprocessa a mensagem, a transicao e contada duas vezes, embora a OS e a saga mudem uma vez so.
// Este teste DOCUMENTA essa limitacao conhecida contra SQL Server e RabbitMQ reais, para ela nao ser descoberta por acaso: a
// contagem pode ficar a maior, nunca a menor, e os gauges (que leem o banco) continuam exatos. Se um dia a emissao passar a depois
// do commit, este teste deve passar a afirmar 1 e a limitacao sair do ADR.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class SagaMetricasNoCommitComSqlTests
{
    private static readonly (string, string?)[] RetryRapido =
    {
        ("Mensageria:RetryIntervalosMs:0", "100"), ("Mensageria:RetryIntervalosMs:1", "100"), ("Mensageria:RetryIntervalosMs:2", "100")
    };

    private readonly InfraFixture _infra;

    public SagaMetricasNoCommitComSqlTests(InfraFixture infra) => _infra = infra;

    [Fact]
    public async Task CommitQueFalhaUmaVezEmConcluida_DocumentaALimitacaoConhecidaDeContarAMetricaDeTransicaoDuasVezes()
    {
        MetricasDaSaga.Limpar();
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        var interceptor = new FalhaNoCommitDaSagaEmEstadoInterceptor("Concluida");
        await using var ambiente = await AmbienteDaSagaInfra.CriarAsync(
            _infra,
            servicos => servicos.AddDbContext<AppDbContext>(opcoes => opcoes.AddInterceptors(interceptor)),
            true,
            RetryRapido);

        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");
        await ambiente.AprovarAsync(ordem.OrdemId);
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoPagamento");
        (await Esperar.AteAsync(async () => (await ambiente.OrdemAsync(ordem.OrdemId)).PagamentoId != null, TimeSpan.FromSeconds(30)))
            .Should().BeTrue("o Billing simulado deveria ter respondido o link de pagamento");
        var pagamentoId = ComportamentoDosServicos.PagamentoDa(ordem.OrdemId);
        await ambiente.PublicarAsync(new PagamentoAprovadoBuilder().DaOrdem(ordem.OrdemId).ComPagamento(pagamentoId).ComValor(270m).Build());
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "EmExecucao");

        interceptor.Ligado = true;
        await ambiente.PublicarAsync(new ExecucaoFinalizadaBuilder().DaOrdem(ordem.OrdemId).ComResumo("Serviço concluído.").Build());
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "Concluida");
        (await Esperar.AteAsync(
            async () => (await ambiente.OrdemAsync(ordem.OrdemId)).Status == StatusOrdemDeServico.Finalizada, TimeSpan.FromSeconds(10)))
            .Should().BeTrue("a OS deveria estar Finalizada");

        // O efeito de negocio acontece uma vez so, mesmo com o commit refeito.
        interceptor.Falhas.Should().Be(1, "o commit da primeira tentativa falhou de verdade");
        interceptor.CommitsComASagaEmConcluida.Should().BeGreaterThanOrEqualTo(2, "a segunda tentativa (o retry) comitou");
        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        os.HistoricoStatus.Count(h => h.Status == StatusOrdemDeServico.Finalizada).Should().Be(1);
        (await ambiente.EstadoDaSagaAsync(ordem.OrdemId)).Should().Be("Concluida");
        (await ambiente.Infra.ConsultarAsync(db => db.Set<OrdemDeServicoSagaState>().CountAsync(s => s.CorrelationId == ordem.OrdemId)))
            .Should().Be(1, "a saga e uma linha so");
        (await ambiente.MensagensEmErroAsync()).Should().Be(0, "o retry resolveu");

        // Limitacao conhecida: a emissao e anterior ao commit, entao a transicao foi contada nas DUAS tentativas.
        metricas.Medicoes("saga_concluida").Sum(m => m.Valor).Should().Be(2, "contagem a maior por commit que falhou e foi refeito (limitacao conhecida, ADR 0008)");
        metricas.Medicoes("saga_duracao_estado_segundos").Count(m => m.Tag("estado") == "EmExecucao")
            .Should().Be(2, "a duracao tambem foi registrada nas duas tentativas");

        // O gauge parte do banco e continua exato: uma saga Concluida.
        await using (var escopo = ambiente.Infra.CriarEscopo())
            await escopo.ServiceProvider.GetRequiredService<ColetorDeMetricasDaSaga>().ColetarAsync();
        metricas.LerGauges();
        metricas.Medicoes("saga_por_estado").Single(m => m.Tag("estado") == "Concluida").Valor.Should().Be(1);
        MetricasDaSaga.Limpar();
    }

    // Derruba o commit da transacao que traz a saga no estado pedido, uma unica vez. Nesse ponto os passos ja rodaram e o observador
    // ja emitiu; o rollback leva OS, saga e mensagens, e o retry refaz tudo.
    private sealed class FalhaNoCommitDaSagaEmEstadoInterceptor : DbTransactionInterceptor
    {
        private readonly string _estado;
        private int _commitsComASaga;
        private int _falhas;

        public FalhaNoCommitDaSagaEmEstadoInterceptor(string estado) => _estado = estado;

        public volatile bool Ligado;

        public int CommitsComASagaEmConcluida => _commitsComASaga;

        public int Falhas => _falhas;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            var trazASagaNoEstado = eventData.Context is { } contexto
                && contexto.ChangeTracker.Entries<OrdemDeServicoSagaState>().Any(e => e.Entity.CurrentState == _estado);

            if (Ligado && trazASagaNoEstado && Interlocked.Increment(ref _commitsComASaga) == 1)
            {
                Interlocked.Increment(ref _falhas);
                throw new FalhaTecnicaSimuladaException();
            }

            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }
}
