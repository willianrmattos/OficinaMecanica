using System.Diagnostics.Metrics;

namespace OficinaMecanica.Application.Observabilidade;

// Nome registrado via AddMeter("OficinaMecanica.API") no Program.cs - sem esse
// registro no MeterProvider, os instrumentos abaixo sao criados normalmente mas
// nao saem via OTLP.
public static class MetricasNegocio
{
    public const string MeterName = "OficinaMecanica.API";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> OrdensCriadas =
        Meter.CreateCounter<long>("ordens_criadas_total");

    public static readonly Counter<long> OrcamentosRecusados =
        Meter.CreateCounter<long>("orcamentos_recusados_total");

    public static readonly Counter<long> EmailsFalha =
        Meter.CreateCounter<long>("emails_falha_total");

    public static readonly Histogram<double> TempoExecucaoHoras =
        Meter.CreateHistogram<double>("tempo_execucao_horas", unit: "h");
}
