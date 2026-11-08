using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// A traducao das consultas agregadas do coletor de metricas (GroupBy + Contains + comparacao de datetimeoffset) e da leitura da
// linha da saga para SQL Server de verdade: o EF InMemory aceita expressoes que o provider relacional recusa ou traduz diferente.
// O retrato do coletor e estatico (como o Meter): este teste so afirma sobre os gauges, que so o coletor escreve, e os testes que
// emitem contadores rodam em colecao propria sem paralelismo, entao nada aqui disputa valor com eles.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class ColetorDeMetricasEConsultaDaSagaComSqlTests
{
    private readonly InfraFixture _infra;

    public ColetorDeMetricasEConsultaDaSagaComSqlTests(InfraFixture infra) => _infra = infra;

    private static OrdemDeServicoSagaState Saga(string estado, DateTimeOffset atualizadaEm) => new()
    {
        CorrelationId = Guid.NewGuid(),
        CurrentState = estado,
        Numero = "OS-" + Guid.NewGuid().ToString("N")[..8],
        CriadaEm = atualizadaEm,
        AtualizadaEm = atualizadaEm
    };

    private Task<AmbienteDeInfra> CriarAmbienteAsync(string limiteDeCompensacaoPresaEmMinutos = "30") =>
        AmbienteDeInfra.CriarAsync(
            _infra,
            configuracaoExtra: new (string, string?)[] { ("Saga:SweeperHabilitado", "false"), ("Saga:CompensacaoPresaMinutos", limiteDeCompensacaoPresaEmMinutos) },
            compor: (servicos, cfg) => servicos.AddServicosDaSaga(cfg));

    [Fact]
    public async Task Coletar_ContraSqlServer_DeveTraduzirAsConsultasECompararOLimiteComODatetimeoffset()
    {
        await using var ambiente = await CriarAmbienteAsync("30");
        var agora = DateTimeOffset.UtcNow;
        await ambiente.SemearAsync(
            Saga("AguardandoPagamento", agora.AddDays(-3)),
            Saga("AguardandoPagamento", agora),
            Saga("CancelandoPagamento", agora.AddMinutes(-31)),
            Saga("CancelandoPagamento", agora.AddMinutes(-29)),
            Saga("Estornando", agora.AddHours(-5)),
            Saga("EstornoPendenteManual", agora.AddDays(-2)),
            Saga("Concluida", agora.AddDays(-9)));

        MetricasDaSaga.Limpar();
        await using (var escopo = ambiente.CriarEscopo())
            await escopo.ServiceProvider.GetRequiredService<ColetorDeMetricasDaSaga>().ColetarAsync();

        using var metricas = new CapturadorDeMetricas(MetricasDaSaga.MeterName);
        metricas.LerGauges();

        double Valor(string instrumento, string estado) => metricas.Medicoes(instrumento).Single(m => m.Tag("estado") == estado).Valor;

        Valor("saga_por_estado", "AguardandoPagamento").Should().Be(2);
        Valor("saga_por_estado", "CancelandoPagamento").Should().Be(2);
        Valor("saga_por_estado", "Concluida").Should().Be(1);
        Valor("saga_compensacao_presa", "CancelandoPagamento").Should().Be(1, "so a parada ha 31 minutos passa do limite de 30");
        Valor("saga_compensacao_presa", "Estornando").Should().Be(1);
        Valor("saga_compensacao_presa", "EstornoPendenteManual").Should().Be(1);
        MetricasDaSaga.Limpar();
    }

    [Fact]
    public async Task LerEstadoDaSaga_ContraSqlServer_DeveDevolverOEstadoEOCarimboDaLinhaDaOs()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var atualizadaEm = new DateTimeOffset(2026, 3, 10, 15, 30, 0, TimeSpan.Zero);
        var saga = Saga("EmExecucao", atualizadaEm);
        await ambiente.SemearAsync(saga, Saga("Concluida", atualizadaEm));

        await using var escopo = ambiente.CriarEscopo();
        var leitor = escopo.ServiceProvider.GetRequiredService<ISagaStateReader>();

        (await leitor.ObterAsync(saga.CorrelationId)).Should().Be(new EstadoConhecidoDaSaga("EmExecucao", atualizadaEm));
        (await leitor.ObterAsync(Guid.NewGuid())).Should().BeNull("OS sem linha de saga (anterior a ela)");
    }
}
