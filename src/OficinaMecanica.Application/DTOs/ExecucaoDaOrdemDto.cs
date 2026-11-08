namespace OficinaMecanica.Application.DTOs;

// Resposta de GET /api/ordens-de-servico/{id}/execucao. Origem diz de onde vem o detalhe: "execucao" (a Execucao respondeu na
// hora) ou "saga" (a Execucao nao esta disponivel ou ainda nao tem a ordem, e o OS Service devolve o que ele proprio sabe).
// No fallback so existem dados que o OS Service tem: nada da producao e inventado, Producao fica nula.
public record ExecucaoDaOrdemDto(
    Guid OrdemDeServicoId,
    string Numero,
    string Origem,
    string StatusDaOrdem,
    string? EstadoDaSaga,
    DateTimeOffset? EstadoDaSagaAtualizadoEm,
    string? MotivoDoFallback,
    ProducaoDaOrdemDto? Producao);

public static class OrigemDaConsultaDeExecucao
{
    public const string Execucao = "execucao";
    public const string Saga = "saga";
}

public static class MotivoDoFallbackDaExecucao
{
    // A Execucao respondeu 404: a ordem de producao so existe depois de ela consumir o SolicitarDiagnostico.
    public const string ProducaoNaoEnfileirada = "ProducaoNaoEnfileirada";

    // Timeout, circuito aberto, 5xx, falha de rede ou resposta que o OS Service nao conseguiu ler.
    public const string ExecucaoIndisponivel = "ExecucaoIndisponivel";
}

// Modelo PROPRIO do OS Service para o que a Execucao devolve (camada anticorrupcao): nao referencia nenhum tipo da Execucao e
// leva so o que o OS Service decidiu expor. Se a Execucao mudar o contrato dela, so o mapeamento na Infrastructure muda.
public record ProducaoDaOrdemDto(
    string Status,
    string? MecanicoId,
    LaudoDaProducaoDto? Laudo,
    string? Resumo,
    string? MotivoInviabilidade,
    string? MotivoCancelamento,
    DateTimeOffset EnfileiradaEm,
    DateTimeOffset? LiberadaEm,
    IReadOnlyList<EtapaDaProducaoDto> Etapas,
    IReadOnlyList<ApontamentoDaProducaoDto> Apontamentos);

public record LaudoDaProducaoDto(string Texto, decimal? HorasEstimadas);

public record EtapaDaProducaoDto(string Nome, string? MecanicoId, string Status, DateTimeOffset Inicio, DateTimeOffset? Fim);

public record ApontamentoDaProducaoDto(string? MecanicoId, string Texto, DateTimeOffset Em);
