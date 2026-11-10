using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Interfaces;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace OficinaMecanica.Infrastructure.Execucao;

// Cliente HTTP tipado da consulta sincrona a Execucao. Timeout, retry e circuit breaker ficam no pipeline de resiliencia do
// HttpClient (ver ExecucaoExtensions); aqui so se traduz o resultado final para a porta: encontrou, nao existe (404) ou
// indisponivel. Indisponivel cobre tudo o que nao e uma resposta util: 5xx depois dos retries, timeout, circuito aberto, falha de
// rede, 401/403 ou qualquer outro 4xx (o token repassado nao serve: nunca se repete) e corpo ilegivel.
public sealed class ExecucaoHttpClient : IExecucaoClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<ExecucaoHttpClient> _logger;

    public ExecucaoHttpClient(HttpClient http, ILogger<ExecucaoHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ResultadoDaConsultaAExecucao> ObterOrdemDeProducaoAsync(Guid ordemDeServicoId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var resposta = await _http.GetAsync($"api/ordens-de-producao/{ordemDeServicoId}", cancellationToken);

            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return ResultadoDaConsultaAExecucao.NaoEncontrada();

            if (!resposta.IsSuccessStatusCode)
                return Indisponivel(ordemDeServicoId, $"HTTP {(int)resposta.StatusCode}");

            var externa = await resposta.Content.ReadFromJsonAsync<OrdemDeProducaoExterna>(Json, cancellationToken);
            if (externa is null)
                return Indisponivel(ordemDeServicoId, "resposta vazia");

            // A resposta precisa ser da ordem que se perguntou: um 200 com a producao de OUTRA OS (proxy ou cache trocando a
            // resposta, bug do lado de la) apareceria na consulta desta OS. Descarto e caio no fallback, logando so os dois ids.
            if (externa.Id != ordemDeServicoId)
            {
                _logger.LogWarning(
                    "A Execução respondeu à consulta da ordem de serviço {OrdemDeServicoId} com a ordem {OrdemRecebidaId}: resposta descartada.",
                    ordemDeServicoId,
                    externa.Id);
                return ResultadoDaConsultaAExecucao.Indisponivel("resposta de outra ordem");
            }

            return ResultadoDaConsultaAExecucao.Encontrada(Mapear(externa));
        }
        // Cancelamento pedido por quem chamou (ex.: o usuario fechou a conexao) nao e indisponibilidade da Execucao.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BrokenCircuitException)
        {
            return Indisponivel(ordemDeServicoId, "circuito aberto");
        }
        catch (TimeoutRejectedException)
        {
            return Indisponivel(ordemDeServicoId, "timeout");
        }
        catch (OperationCanceledException)
        {
            return Indisponivel(ordemDeServicoId, "timeout");
        }
        catch (HttpRequestException ex)
        {
            return Indisponivel(ordemDeServicoId, $"falha de rede ({ex.GetType().Name})");
        }
        catch (JsonException)
        {
            return Indisponivel(ordemDeServicoId, "resposta ilegível");
        }
    }

    private ResultadoDaConsultaAExecucao Indisponivel(Guid ordemDeServicoId, string detalhe)
    {
        _logger.LogWarning("A Execução não atendeu a consulta da ordem de serviço {OrdemDeServicoId}: {Detalhe}.", ordemDeServicoId, detalhe);
        return ResultadoDaConsultaAExecucao.Indisponivel(detalhe);
    }

    private static ProducaoDaOrdemDto Mapear(OrdemDeProducaoExterna externa) =>
        new(
            externa.Status ?? string.Empty,
            externa.MecanicoId,
            externa.Laudo is null ? null : new LaudoDaProducaoDto(externa.Laudo.Texto ?? string.Empty, externa.Laudo.HorasEstimadas),
            externa.Resumo,
            externa.MotivoInviabilidade,
            externa.MotivoCancelamento,
            externa.EnfileiradaEm,
            externa.LiberadaEm,
            (externa.Etapas ?? new()).Select(e => new EtapaDaProducaoDto(e.Nome ?? string.Empty, e.MecanicoId, e.Status ?? string.Empty, e.Inicio, e.Fim)).ToList(),
            (externa.Apontamentos ?? new()).Select(a => new ApontamentoDaProducaoDto(a.MecanicoId, a.Texto ?? string.Empty, a.Em)).ToList());
}
