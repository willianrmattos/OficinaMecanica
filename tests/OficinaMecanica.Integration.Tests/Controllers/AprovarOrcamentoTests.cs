using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class AprovarOrcamentoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AprovarOrcamentoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task<HttpResponseMessage> Aprovar(Guid id) =>
        _client.PostAsync($"/api/ordens-de-servico/{id}/aprovar", content: null);

    private Task<StatusOrdemDeServico> StatusNoBanco(Guid id) =>
        _factory.ConsultarAsync(db => db.OrdensDeServico.Where(o => o.Id == id).Select(o => o.Status).SingleAsync());

    [Fact]
    public async Task Aprovar_OrdemAguardandoAprovacao_DeveResponder202ComStatusAtualEPublicarADecisao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        var response = await Aprovar(ordem.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("ordemDeServicoId").GetGuid().Should().Be(ordem.Id);
        corpo.GetProperty("numero").GetString().Should().Be(ordem.Numero);
        corpo.GetProperty("status").GetString().Should().Be("AguardandoAprovacao");
        _factory.Publicador.Publicadas<ClienteAprovouOrcamento>()
            .Should().ContainSingle(m => m.OrdemDeServicoId == ordem.Id);
    }

    [Fact]
    public async Task Aprovar_DeveManterOStatusPoisQuemMudaEASaga()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        await Aprovar(ordem.Id);

        (await StatusNoBanco(ordem.Id)).Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
    }

    [Fact]
    public async Task Aprovar_SemToken_DeveSerPermitidoPorSerEndpointPublico()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        var response = await Aprovar(ordem.Id);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Aprovar_OrdemAnteriorASaga_DeveRetornar409ENaoPublicar()
    {
        var ordem = new OrdemDeServicoBuilder().AnteriorASaga().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        var response = await Aprovar(ordem.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("tipo").GetString().Should().Be("Conflito");
        corpo.GetProperty("mensagem").GetString().Should().Contain("antes do fluxo");
        _factory.Publicador.Publicadas<ClienteAprovouOrcamento>().Should().NotContain(m => m.OrdemDeServicoId == ordem.Id);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public async Task Aprovar_OrdemForaDeAguardandoAprovacao_DeveRetornar409ENaoPublicar(StatusOrdemDeServico status)
    {
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        await _factory.SemearAsync(ordem);

        var response = await Aprovar(ordem.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("mensagem").GetString().Should().Contain(status.ToString());
        _factory.Publicador.Publicadas<ClienteAprovouOrcamento>().Should().NotContain(m => m.OrdemDeServicoId == ordem.Id);
    }

    [Fact]
    public async Task Aprovar_OrdemInexistente_DeveRetornar400()
    {
        var response = await Aprovar(Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
