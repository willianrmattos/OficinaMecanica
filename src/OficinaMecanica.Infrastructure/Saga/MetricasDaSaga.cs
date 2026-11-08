using System.Diagnostics.Metrics;

namespace OficinaMecanica.Infrastructure.Saga;

// Metricas da saga no mesmo padrao do MetricasNegocio (System.Diagnostics.Metrics; o Meter e registrado no OpenTelemetry por
// AddMeter(MeterName) no Program.cs, sem esse registro os instrumentos existem mas nao saem via OTLP). Os contadores contam
// TRANSICOES JA CONCLUIDAS pela maquina (o observador roda depois de todos os passos da transicao) e os passos que apenas
// avisam (falha de compensacao, decisao ignorada) emitem junto do log que ja os acompanha. Nenhuma tag leva id de OS, numero
// ou texto livre: so valores de enum, nomes de estado, de evento e de comando (cardinalidade baixa e sem dado pessoal).
public static class MetricasDaSaga
{
    public const string MeterName = "OficinaMecanica.Saga";

    private static readonly Meter Meter = new(MeterName);

    // Estados em que a saga espera um servico externo responder uma compensacao ou uma pessoa agir: sem decisao automatica,
    // so o alerta (ADR 0003 e 0008). A saga parada ali alem do limite e o sinal de "saga presa".
    public static readonly IReadOnlyList<string> EstadosDeCompensacao = new[] { "CancelandoPagamento", "Estornando", "EstornoPendenteManual" };

    // Inicia sem tag: e o volume de OS que entraram na saga.
    public static readonly Counter<long> Iniciada =
        Meter.CreateCounter<long>("saga_iniciada", description: "Sagas criadas (ordem de servico aberta).");

    public static readonly Counter<long> Concluida =
        Meter.CreateCounter<long>("saga_concluida", description: "Sagas que terminaram com o reparo finalizado.");

    // Tag estado_origem: de que estado a saga saiu para Cancelada (ex.: AguardandoPagamento, Estornando).
    public static readonly Counter<long> Cancelada =
        Meter.CreateCounter<long>("saga_cancelada", description: "Sagas encerradas em Cancelada, por estado de origem.");

    // Tag causa: o evento que a levou ao estado (ClienteRecusouOrcamento ou PrazoExpirado).
    public static readonly Counter<long> OrcamentoRecusado =
        Meter.CreateCounter<long>("saga_orcamento_recusado", description: "Sagas encerradas em OrcamentoRecusado, por causa.");

    // Tag causa: EstornoFalhou (o Billing respondeu a falha) ou Fault<EstornarPagamento> (falha tecnica do comando).
    public static readonly Counter<long> EstornoPendenteManual =
        Meter.CreateCounter<long>("saga_estorno_pendente_manual", description: "Sagas que pararam em EstornoPendenteManual: o dinheiro nao voltou e alguem precisa agir.");

    // Tag comando: CancelarPagamento, CancelarOrcamento ou CancelarProducao. Um por Fault<T>.
    public static readonly Counter<long> CompensacaoFalhou =
        Meter.CreateCounter<long>("saga_compensacao_falhou", description: "Falhas tecnicas de comandos de compensacao (sem reenvio automatico: o comando espera o redrive da fila de erro).");

    // Tags decisao (ClienteAprovouOrcamento, ClienteRecusouOrcamento, CancelamentoSolicitado) e estado em que a saga estava.
    public static readonly Counter<long> DecisaoIgnorada =
        Meter.CreateCounter<long>("saga_decisao_ignorada", description: "Decisoes de uma pessoa aceitas pela API que a saga nao podia mais aplicar.");

    // Tag tipo: Aprovacao ou Pagamento. So conta o prazo que decidiu algo (o vigente); o substituido ou ja decidido e ignorado.
    public static readonly Counter<long> PrazoExpirado =
        Meter.CreateCounter<long>("saga_prazo_expirado", description: "Prazos vencidos que de fato mudaram o estado da saga.");

    // Tag estado: o estado que a saga deixou. Medida entre a ultima atualizacao da saga (AtualizadaEm, lida antes de o evento
    // ser processado) e o carimbo da transicao; ver ObservadorDeMetricasDaSaga.
    public static readonly Histogram<double> DuracaoDoEstado =
        Meter.CreateHistogram<double>(
            "saga_duracao_estado_segundos",
            unit: "s",
            description: "Tempo que a saga ficou num estado antes de sair dele.",
            tags: null,
            advice: new InstrumentAdvice<double>
            {
                // Os estados vao de segundos (resposta de um servico) a dias (cliente decidindo): as fronteiras padrao do
                // OpenTelemetry (ate 10 000) nao distinguem nada acima de uma hora.
                HistogramBucketBoundaries = new double[] { 1, 5, 30, 60, 300, 900, 3600, 14400, 43200, 86400, 259200, 604800 }
            });

    // Os gauges observaveis leem o ultimo retrato coletado do banco (ColetorDeMetricasDaSaga): a leitura do banco e assincrona
    // e o callback do instrumento e sincrono, entao o callback so devolve o que ja foi coletado.
    private static volatile InstantaneoDasSagas _instantaneo = InstantaneoDasSagas.Vazio;

    private static readonly ObservableGauge<long> PorEstado = Meter.CreateObservableGauge(
        "saga_por_estado",
        () => _instantaneo.PorEstado.Select(par => new Measurement<long>(par.Value, new KeyValuePair<string, object?>("estado", par.Key))),
        description: "Quantidade de sagas em cada estado (ultima coleta). Com varias replicas todas exportam o mesmo valor: agregar com max, nunca sum.");

    private static readonly ObservableGauge<long> CompensacaoPresa = Meter.CreateObservableGauge(
        "saga_compensacao_presa",
        () => _instantaneo.CompensacaoPresa.Select(par => new Measurement<long>(par.Value, new KeyValuePair<string, object?>("estado", par.Key))),
        description: "Sagas em estado de compensacao sem atualizacao ha mais que o limite (Saga__CompensacaoPresaMinutos), por estado. Acima de zero e alerta.");

    public static void Atualizar(InstantaneoDasSagas instantaneo) => _instantaneo = instantaneo;

    // Fora de uma coleta nao ha o que informar: o gauge some em vez de afirmar zero.
    public static void Limpar() => _instantaneo = InstantaneoDasSagas.Vazio;
}

// Retrato imutavel lido do banco: quantas sagas ha em cada estado e quantas, nos estados de compensacao, estao paradas.
public sealed record InstantaneoDasSagas(
    IReadOnlyDictionary<string, long> PorEstado,
    IReadOnlyDictionary<string, long> CompensacaoPresa)
{
    public static readonly InstantaneoDasSagas Vazio = new(new Dictionary<string, long>(), new Dictionary<string, long>());
}
