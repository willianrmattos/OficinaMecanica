using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class SwaggerOrdensDeServicoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SwaggerOrdensDeServicoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<JsonElement> ObterOperacaoAsync(string caminho, string metodo)
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var documento = await response.Content.ReadFromJsonAsync<JsonElement>();
        return documento.GetProperty("paths").GetProperty(caminho).GetProperty(metodo);
    }

    private static string[] CodigosDeResposta(JsonElement operacao) =>
        operacao.GetProperty("responses").EnumerateObject().Select(r => r.Name).ToArray();

    [Theory]
    [InlineData("/api/ordens-de-servico/{id}/aprovar")]
    [InlineData("/api/ordens-de-servico/{id}/recusar")]
    public async Task AprovarERecusar_DevemDocumentar202E409ENaoMais200(string caminho)
    {
        var operacao = await ObterOperacaoAsync(caminho, "post");

        CodigosDeResposta(operacao).Should().BeEquivalentTo("202", "400", "409");
    }

    [Fact]
    public async Task Cancelar_DeveDocumentar202_401E409()
    {
        var operacao = await ObterOperacaoAsync("/api/ordens-de-servico/{id}/cancelar", "post");

        CodigosDeResposta(operacao).Should().BeEquivalentTo("202", "400", "401", "409");
        operacao.GetProperty("operationId").GetString().Should().Be("OrdensDeServico_Cancelar");
    }

    [Fact]
    public async Task AprovarOrcamento_Resposta202_DeveReferenciarOCorpoDeSolicitacaoAceita()
    {
        var operacao = await ObterOperacaoAsync("/api/ordens-de-servico/{id}/aprovar", "post");

        var esquema = operacao.GetProperty("responses").GetProperty("202")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        esquema.GetProperty("$ref").GetString().Should().EndWith("SolicitacaoAceitaDto");
    }
}
