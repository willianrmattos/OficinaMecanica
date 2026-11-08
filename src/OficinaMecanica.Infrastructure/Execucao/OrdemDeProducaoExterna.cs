namespace OficinaMecanica.Infrastructure.Execucao;

// O que a Execucao devolve em GET /api/ordens-de-producao/{id}, como o OS Service o le. Declaro so os campos que o OS Service
// expoe: o resto do JSON (veiculo, servicos, pecas, quem inviabilizou) e ignorado de proposito, entao um campo novo ou removido
// la fora so quebra aqui se for um dos que usamos. Nenhum tipo da Execucao e referenciado: o contrato e a forma do JSON.
internal sealed class OrdemDeProducaoExterna
{
    // Conferido contra a ordem pedida: so serve se a resposta for da mesma OS.
    public Guid Id { get; set; }
    public string? Status { get; set; }
    public string? MecanicoId { get; set; }
    public LaudoExterno? Laudo { get; set; }
    public string? Resumo { get; set; }
    public string? MotivoInviabilidade { get; set; }
    public string? MotivoCancelamento { get; set; }
    public DateTimeOffset EnfileiradaEm { get; set; }
    public DateTimeOffset? LiberadaEm { get; set; }
    public List<EtapaExterna>? Etapas { get; set; }
    public List<ApontamentoExterno>? Apontamentos { get; set; }

    internal sealed class LaudoExterno
    {
        public string? Texto { get; set; }
        public decimal? HorasEstimadas { get; set; }
    }

    internal sealed class EtapaExterna
    {
        public string? Nome { get; set; }
        public string? MecanicoId { get; set; }
        public string? Status { get; set; }
        public DateTimeOffset Inicio { get; set; }
        public DateTimeOffset? Fim { get; set; }
    }

    internal sealed class ApontamentoExterno
    {
        public string? MecanicoId { get; set; }
        public string? Texto { get; set; }
        public DateTimeOffset Em { get; set; }
    }
}
