using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class RecusarOrcamentoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RecusarOrcamentoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RecusarOrcamento_OrdemInexistente_DeveRetornar400()
    {
        var response = await _client.PostAsJsonAsync($"/api/ordens-de-servico/{Guid.NewGuid()}/recusar", new { Motivo = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecusarOrcamento_OrdemAguardandoAprovacao_DeveResponder202EPublicarARecusaComMotivo()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        var response = await _client.PostAsJsonAsync($"/api/ordens-de-servico/{ordem.Id}/recusar", new { Motivo = "Achei caro." });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("ordemDeServicoId").GetGuid().Should().Be(ordem.Id);
        corpo.GetProperty("status").GetString().Should().Be("AguardandoAprovacao");
        _factory.Publicador.Publicadas<ClienteRecusouOrcamento>()
            .Should().ContainSingle(m => m.OrdemDeServicoId == ordem.Id && m.Motivo == "Achei caro.");
        var statusNoBanco = await _factory.ConsultarAsync(db => db.OrdensDeServico.Where(o => o.Id == ordem.Id).Select(o => o.Status).SingleAsync());
        statusNoBanco.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
    }

    [Fact]
    public async Task RecusarOrcamento_SemCorpo_DeveAceitarEPublicarSemMotivo()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        var response = await _client.PostAsync($"/api/ordens-de-servico/{ordem.Id}/recusar", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _factory.Publicador.Publicadas<ClienteRecusouOrcamento>()
            .Should().ContainSingle(m => m.OrdemDeServicoId == ordem.Id && m.Motivo == null);
    }

    [Fact]
    public async Task RecusarOrcamento_MotivoMaiorQueOLimite_DeveRetornar400ENaoPublicar()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        var response = await _client.PostAsJsonAsync($"/api/ordens-de-servico/{ordem.Id}/recusar", new { Motivo = new string('x', 401) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Publicador.Publicadas<ClienteRecusouOrcamento>().Should().NotContain(m => m.OrdemDeServicoId == ordem.Id);
    }

    [Fact]
    public async Task RecusarOrcamento_OrdemAnteriorASaga_DeveRetornar409ENaoPublicar()
    {
        var ordem = new OrdemDeServicoBuilder().AnteriorASaga().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        var response = await _client.PostAsJsonAsync($"/api/ordens-de-servico/{ordem.Id}/recusar", new { Motivo = "Motivo." });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _factory.Publicador.Publicadas<ClienteRecusouOrcamento>().Should().NotContain(m => m.OrdemDeServicoId == ordem.Id);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public async Task RecusarOrcamento_OrdemForaDeAguardandoAprovacao_DeveRetornar409(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        await _factory.SemearAsync(ordem);

        var response = await _client.PostAsJsonAsync($"/api/ordens-de-servico/{ordem.Id}/recusar", new { Motivo = "Motivo." });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _factory.Publicador.Publicadas<ClienteRecusouOrcamento>().Should().NotContain(m => m.OrdemDeServicoId == ordem.Id);
    }
}
