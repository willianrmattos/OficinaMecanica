using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// Os gauges leem o ultimo retrato que o coletor leu do banco do proprio servico: quantas sagas ha em cada estado e quantas, nos
// estados de compensacao, estao paradas alem do limite. Aqui o banco e o EF InMemory; a traducao das consultas para SQL Server
// esta em Infra/ColetorDeMetricasDaSagaComSqlTests.
[Collection(ColecaoDeMetricas.Nome)]
public class ColetorDeMetricasDaSagaTests
{
    private static OrdemDeServicoSagaState Saga(string estado, DateTimeOffset atualizadaEm) => new()
    {
        CorrelationId = Guid.NewGuid(),
        CurrentState = estado,
        Numero = "OS-" + Guid.NewGuid().ToString("N")[..8],
        CriadaEm = atualizadaEm,
        AtualizadaEm = atualizadaEm
    };

    private static async Task SemearAsync(AmbienteDaSaga ambiente, params OrdemDeServicoSagaState[] sagas) =>
        await ambiente.ConsultarAsync(async db =>
        {
            db.Set<OrdemDeServicoSagaState>().AddRange(sagas);
            return await db.SaveChangesAsync();
        });

    private static async Task ColetarAsync(AmbienteDaSaga ambiente) =>
        await ambiente.NoEscopoAsync(async sp =>
        {
            await sp.GetRequiredService<ColetorDeMetricasDaSaga>().ColetarAsync();
            return 0;
        });

    private static double Valor(CapturadorDeMetricas metricas, string instrumento, string estado) =>
        metricas.Medicoes(instrumento).Single(m => m.Tag("estado") == estado).Valor;

    [Fact]
    public async Task Coletar_DeveContarAsSagasPorEstadoEAsPresasEmCompensacaoAlemDoLimite()
    {
        MetricasDaSaga.Limpar();
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var ambiente = await AmbienteDaSaga.CriarAsync(configuracao: ("Saga:CompensacaoPresaMinutos", "30"));
        var agora = ambiente.Relogio.GetUtcNow();
        await SemearAsync(
            ambiente,
            Saga("AguardandoPagamento", agora.AddDays(-3)),
            Saga("AguardandoPagamento", agora),
            Saga("EmExecucao", agora.AddHours(-1)),
            Saga("CancelandoPagamento", agora.AddMinutes(-31)),
            Saga("CancelandoPagamento", agora.AddMinutes(-29)),
            Saga("Estornando", agora.AddHours(-5)),
            Saga("EstornoPendenteManual", agora.AddDays(-2)),
            Saga("EstornoPendenteManual", agora.AddDays(-1)),
            Saga("Concluida", agora.AddDays(-9)));

        await ColetarAsync(ambiente);
        metricas.LerGauges();

        Valor(metricas, "saga_por_estado", "AguardandoPagamento").Should().Be(2);
        Valor(metricas, "saga_por_estado", "EmExecucao").Should().Be(1);
        Valor(metricas, "saga_por_estado", "CancelandoPagamento").Should().Be(2);
        Valor(metricas, "saga_por_estado", "Estornando").Should().Be(1);
        Valor(metricas, "saga_por_estado", "EstornoPendenteManual").Should().Be(2);
        Valor(metricas, "saga_por_estado", "Concluida").Should().Be(1);
        metricas.Medicoes("saga_por_estado").Should().HaveCount(6);

        // So a CancelandoPagamento parada ha 31 min passa do limite de 30; a de 29 min ainda esta dentro. Concluida e
        // AguardandoPagamento (parada ha dias, mas esperando o cliente) nao sao compensacao.
        Valor(metricas, "saga_compensacao_presa", "CancelandoPagamento").Should().Be(1);
        Valor(metricas, "saga_compensacao_presa", "Estornando").Should().Be(1);
        Valor(metricas, "saga_compensacao_presa", "EstornoPendenteManual").Should().Be(2);
        metricas.Medicoes("saga_compensacao_presa").Should().HaveCount(3);
    }

    [Fact]
    public async Task Coletar_SemNenhumaPresa_DeveInformarZeroEmCadaEstadoDeCompensacaoParaOAlertaPoderFechar()
    {
        MetricasDaSaga.Limpar();
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var ambiente = await AmbienteDaSaga.CriarAsync();
        await SemearAsync(ambiente, Saga("EmExecucao", ambiente.Relogio.GetUtcNow()));

        await ColetarAsync(ambiente);
        metricas.LerGauges();

        metricas.Medicoes("saga_compensacao_presa").Select(m => (m.Tag("estado"), m.Valor)).Should().BeEquivalentTo(new[]
        {
            ("CancelandoPagamento", 0d), ("Estornando", 0d), ("EstornoPendenteManual", 0d)
        });
    }

    [Fact]
    public async Task Coletar_DeveRespeitarOLimiteConfigurado()
    {
        MetricasDaSaga.Limpar();
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var ambiente = await AmbienteDaSaga.CriarAsync(configuracao: ("Saga:CompensacaoPresaMinutos", "5"));
        var agora = ambiente.Relogio.GetUtcNow();
        await SemearAsync(ambiente, Saga("Estornando", agora.AddMinutes(-6)), Saga("Estornando", agora.AddMinutes(-4)));

        await ColetarAsync(ambiente);
        metricas.LerGauges();

        Valor(metricas, "saga_compensacao_presa", "Estornando").Should().Be(1);
    }

    [Fact]
    public async Task Gauges_AntesDeQualquerColeta_NaoDevemAfirmarNada()
    {
        MetricasDaSaga.Limpar();
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);

        metricas.LerGauges();

        metricas.Medicoes("saga_por_estado").Should().BeEmpty("sem coleta nao ha o que informar, e zero seria uma afirmacao falsa");
        metricas.Medicoes("saga_compensacao_presa").Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Coletar_BancoSemSagas_DeveInformarSoOZeroDasCompensacoes()
    {
        MetricasDaSaga.Limpar();
        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        await using var ambiente = await AmbienteDaSaga.CriarAsync();

        await ColetarAsync(ambiente);
        metricas.LerGauges();

        metricas.Medicoes("saga_por_estado").Should().BeEmpty();
        metricas.Medicoes("saga_compensacao_presa").Should().HaveCount(3).And.OnlyContain(m => m.Valor == 0);
    }
}
