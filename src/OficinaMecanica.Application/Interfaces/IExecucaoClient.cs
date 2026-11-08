using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Interfaces;

// Porta da unica chamada sincrona do OS Service a outro servico de negocio: a consulta somente-leitura da ordem de producao na
// Execucao (ADR 0006). Escrita entre servicos continua sempre assincrona (saga). A implementacao tem timeout, retry e circuit
// breaker e NUNCA lanca por indisponibilidade da Execucao: devolve Indisponivel, e quem chama decide a degradacao. So uma
// excecao de cancelamento pedido pelo proprio chamador (CancellationToken) sai daqui.
public interface IExecucaoClient
{
    Task<ResultadoDaConsultaAExecucao> ObterOrdemDeProducaoAsync(Guid ordemDeServicoId, CancellationToken cancellationToken = default);
}

public enum SituacaoDaConsultaAExecucao
{
    // A Execucao respondeu com a ordem de producao.
    Encontrada,

    // A Execucao respondeu 404: ainda nao existe ordem de producao para essa OS.
    NaoEncontrada,

    // Timeout, circuito aberto, 5xx, falha de rede, resposta recusada (ex.: 401) ou corpo ilegivel.
    Indisponivel
}

public sealed record ResultadoDaConsultaAExecucao(SituacaoDaConsultaAExecucao Situacao, ProducaoDaOrdemDto? Producao = null, string? Detalhe = null)
{
    public static ResultadoDaConsultaAExecucao Encontrada(ProducaoDaOrdemDto producao) => new(SituacaoDaConsultaAExecucao.Encontrada, producao);

    public static ResultadoDaConsultaAExecucao NaoEncontrada() => new(SituacaoDaConsultaAExecucao.NaoEncontrada);

    // O detalhe e so para log (ex.: "HTTP 503", "circuito aberto"): nunca vai para a resposta da API.
    public static ResultadoDaConsultaAExecucao Indisponivel(string detalhe) => new(SituacaoDaConsultaAExecucao.Indisponivel, Detalhe: detalhe);
}
