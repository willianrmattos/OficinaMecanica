using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class OrdensDeServicoControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public OrdensDeServicoControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ObterPorNumero_Inexistente_DeveRetornar404()
    {
        var response = await _client.GetAsync("/api/ordens-de-servico/numero/OS-INEXISTENTE");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ObterTempoMedio_DeveRetornar200()
    {
        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/ordens-de-servico/tempo-medio");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ObterPorNumero_OrdemComPagamentoSolicitado_DeveExporLinkEPrazoSemToken()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var prazo = new DateTimeOffset(2026, 3, 12, 15, 30, 0, TimeSpan.Zero);
        ordem.RegistrarPagamentoSolicitado(Guid.NewGuid(), "https://pagamento.exemplo/checkout/9", prazo);
        await _factory.SemearAsync(ordem);

        var response = await _client.GetAsync($"/api/ordens-de-servico/numero/{ordem.Numero}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("status").GetString().Should().Be("AguardandoPagamento");
        corpo.GetProperty("linkPagamento").GetString().Should().Be("https://pagamento.exemplo/checkout/9");
        corpo.GetProperty("pagamentoExpiraEm").GetDateTimeOffset().Should().Be(prazo);
    }

    [Fact]
    public async Task ObterPorNumero_OrdemSemPagamento_DeveRetornarLinkEPrazoNulos()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        var response = await _client.GetAsync($"/api/ordens-de-servico/numero/{ordem.Numero}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("linkPagamento").ValueKind.Should().Be(JsonValueKind.Null);
        corpo.GetProperty("pagamentoExpiraEm").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static readonly DateTime Inicio = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] FluxoDaOsFinalizada =
        { "Recebida", "EmDiagnostico", "AguardandoAprovacao", "AguardandoPagamento", "EmExecucao", "Finalizada" };

    [Fact]
    public async Task ObterPorId_HistoricoGravadoForaDeOrdem_DeveRetornarEmOrdemCronologica()
    {
        var ordem = new OrdemDeServicoBuilder().Finalizada().ComHistoricoEmOrdemInversa(Inicio);
        await _factory.SemearAsync(ordem);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ObterTokenAsync());

        var response = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var historico = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("historicoStatus").EnumerateArray().ToList();
        historico.Select(h => h.GetProperty("status").GetString()).Should().Equal(FluxoDaOsFinalizada);
        historico.Select(h => h.GetProperty("dataAlteracao").GetDateTime()).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ObterPorNumero_HistoricoGravadoForaDeOrdem_DeveRetornarEmOrdemCronologica()
    {
        var ordem = new OrdemDeServicoBuilder().Finalizada().ComHistoricoEmOrdemInversa(Inicio);
        await _factory.SemearAsync(ordem);

        var response = await _client.GetAsync($"/api/ordens-de-servico/numero/{ordem.Numero}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var historico = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("historicoStatus").EnumerateArray().ToList();
        historico.Select(h => h.GetProperty("status").GetString()).Should().Equal(FluxoDaOsFinalizada);
    }

    [Fact]
    public async Task ObterPorId_TransicoesComAMesmaData_DeveRetornarNaOrdemDoFluxo()
    {
        var ordem = new OrdemDeServicoBuilder().Finalizada().ComHistoricoEmOrdemInversa(Inicio, mesmaData: true);
        await _factory.SemearAsync(ordem);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ObterTokenAsync());

        var response = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var historico = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("historicoStatus").EnumerateArray().ToList();
        historico.Select(h => h.GetProperty("status").GetString()).Should().Equal(FluxoDaOsFinalizada);
    }

    private async Task<string> ObterTokenAsync()
    {
        return await Task.FromResult(TestTokenFactory.GerarToken(_factory));
    }
}
