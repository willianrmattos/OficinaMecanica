using System.Globalization;
using System.Net;
using FluentAssertions;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Middleware;

// O prometheus-net rotula cada requisição com o status que a resposta tem quando o middleware dele termina. O
// UseHttpMetrics fica por fora do middleware de exceções, então um erro de negócio que vira 400 ou 409 precisa aparecer em
// /metrics com esse código, e não como 200. O registro de métricas é do processo inteiro e as classes de teste rodam em
// paralelo, por isso comparo duas leituras de /metrics e exijo que o contador do código esperado tenha crescido.
public class MetricasDeRequisicaoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MetricasDeRequisicaoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<double> RequisicoesAsync(string codigo, string acao)
    {
        var texto = await (await _client.GetAsync("/metrics")).Content.ReadAsStringAsync();
        return texto.Split('\n')
            .Where(l => l.StartsWith("http_requests_received_total{", StringComparison.Ordinal)
                        && l.Contains($"code=\"{codigo}\"") && l.Contains("controller=\"OrdensDeServico\"") && l.Contains($"action=\"{acao}\""))
            .Sum(l => double.Parse(l[(l.LastIndexOf(' ') + 1)..], CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Metrics_DeveResponder200ComAsMetricasHttp()
    {
        var resposta = await _client.GetAsync("/metrics");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("http_requests_received_total");
    }

    [Fact]
    public async Task ErroDeNegocioQueVira400_DeveSerContadoComoCode400NasMetricas()
    {
        var antes = await RequisicoesAsync("400", "AprovarOrcamento");

        var resposta = await _client.PostAsync($"/api/ordens-de-servico/{Guid.NewGuid()}/aprovar", content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RequisicoesAsync("400", "AprovarOrcamento")).Should().BeGreaterThan(antes, "o 400 mapeado pelo middleware aparece como 400 em /metrics");
    }

    [Fact]
    public async Task ConflitoQueVira409_DeveSerContadoComoCode409NasMetricas()
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(StatusOrdemDeServico.Cancelada);
        await _factory.SemearAsync(ordem);
        var antes = await RequisicoesAsync("409", "AprovarOrcamento");

        var resposta = await _client.PostAsync($"/api/ordens-de-servico/{ordem.Id}/aprovar", content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await RequisicoesAsync("409", "AprovarOrcamento")).Should().BeGreaterThan(antes, "o 409 mapeado pelo middleware aparece como 409 em /metrics");
    }
}
