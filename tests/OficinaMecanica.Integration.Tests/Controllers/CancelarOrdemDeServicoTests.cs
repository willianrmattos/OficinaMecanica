using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class CancelarOrdemDeServicoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CancelarOrdemDeServicoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void Autenticar() =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenFactory.GerarToken(_factory));

    private Task<HttpResponseMessage> Cancelar(Guid id, string? motivo = "Cliente desistiu.") =>
        _client.PostAsJsonAsync($"/api/ordens-de-servico/{id}/cancelar", new { Motivo = motivo });

    [Fact]
    public async Task Cancelar_SemToken_DeveRetornar401()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        await _factory.SemearAsync(ordem);

        var response = await Cancelar(ordem.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _factory.Publicador.Publicadas<CancelamentoSolicitado>().Should().NotContain(m => m.OrdemDeServicoId == ordem.Id);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    public async Task Cancelar_AntesDaExecucao_DeveResponder202PublicarESemMudarOStatus(StatusOrdemDeServico status)
    {
        Autenticar();
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        await _factory.SemearAsync(ordem);

        var response = await Cancelar(ordem.Id, "Cliente desistiu.");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("ordemDeServicoId").GetGuid().Should().Be(ordem.Id);
        corpo.GetProperty("status").GetString().Should().Be(status.ToString());
        _factory.Publicador.Publicadas<CancelamentoSolicitado>()
            .Should().ContainSingle(m => m.OrdemDeServicoId == ordem.Id && m.Motivo == "Cliente desistiu.");
        var statusNoBanco = await _factory.ConsultarAsync(db => db.OrdensDeServico.Where(o => o.Id == ordem.Id).Select(o => o.Status).SingleAsync());
        statusNoBanco.Should().Be(status);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Entregue)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public async Task Cancelar_APartirDaExecucaoOuStatusTerminal_DeveRetornar409(StatusOrdemDeServico status)
    {
        Autenticar();
        var ordem = new OrdemDeServicoBuilder().NoStatus(status);
        await _factory.SemearAsync(ordem);

        var response = await Cancelar(ordem.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("tipo").GetString().Should().Be("Conflito");
        _factory.Publicador.Publicadas<CancelamentoSolicitado>().Should().NotContain(m => m.OrdemDeServicoId == ordem.Id);
    }

    [Fact]
    public async Task Cancelar_OrdemAnteriorASaga_DeveRetornar409()
    {
        Autenticar();
        var ordem = new OrdemDeServicoBuilder().AnteriorASaga().AguardandoAprovacao();
        await _factory.SemearAsync(ordem);

        var response = await Cancelar(ordem.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _factory.Publicador.Publicadas<CancelamentoSolicitado>().Should().NotContain(m => m.OrdemDeServicoId == ordem.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Cancelar_SemMotivo_DeveRetornar400ENaoPublicar(string? motivo)
    {
        Autenticar();
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        await _factory.SemearAsync(ordem);

        var response = await Cancelar(ordem.Id, motivo);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Publicador.Publicadas<CancelamentoSolicitado>().Should().NotContain(m => m.OrdemDeServicoId == ordem.Id);
    }

    [Fact]
    public async Task Cancelar_OrdemInexistente_DeveRetornar400()
    {
        Autenticar();

        var response = await Cancelar(Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
