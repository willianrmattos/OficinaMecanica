using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class AtualizarStatusOrdemTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AtualizarStatusOrdemTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void Autenticar() =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenFactory.GerarToken(_factory));

    private Task<HttpResponseMessage> AtualizarStatus(Guid id, int novoStatus) =>
        _client.PatchAsJsonAsync($"/api/ordens-de-servico/{id}/status", new { NovoStatus = novoStatus });

    private Task<StatusOrdemDeServico> StatusNoBanco(Guid id) =>
        _factory.ConsultarAsync(db => db.OrdensDeServico.Where(o => o.Id == id).Select(o => o.Status).SingleAsync());

    [Fact]
    public async Task AtualizarStatus_SemToken_DeveRetornar401()
    {
        var response = await AtualizarStatus(Guid.NewGuid(), (int)StatusOrdemDeServico.Entregue);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AtualizarStatus_EntregueDeOrdemFinalizada_DeveRetornar204EEntregar()
    {
        Autenticar();
        var ordem = new OrdemDeServicoBuilder().Finalizada();
        await _factory.SemearAsync(ordem);

        var response = await AtualizarStatus(ordem.Id, (int)StatusOrdemDeServico.Entregue);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusNoBanco(ordem.Id)).Should().Be(StatusOrdemDeServico.Entregue);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    public async Task AtualizarStatus_QualquerStatusQueNaoSejaEntregue_DeveRetornar400SemAlterarAOrdem(StatusOrdemDeServico novoStatus)
    {
        Autenticar();
        var ordem = new OrdemDeServicoBuilder().Recebida();
        await _factory.SemearAsync(ordem);

        var response = await AtualizarStatus(ordem.Id, (int)novoStatus);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("erros").EnumerateArray()
            .Should().Contain(e => e.GetProperty("campo").GetString() == "NovoStatus");
        (await StatusNoBanco(ordem.Id)).Should().Be(StatusOrdemDeServico.Recebida);
    }

    [Fact]
    public async Task AtualizarStatus_StatusInexistente_DeveRetornar400()
    {
        Autenticar();
        var ordem = new OrdemDeServicoBuilder().Recebida();
        await _factory.SemearAsync(ordem);

        var response = await AtualizarStatus(ordem.Id, 999);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AtualizarStatus_EntregueDeOrdemQueAindaNaoFoiFinalizada_DeveRetornar400()
    {
        Autenticar();
        var ordem = new OrdemDeServicoBuilder().EmExecucao();
        await _factory.SemearAsync(ordem);

        var response = await AtualizarStatus(ordem.Id, (int)StatusOrdemDeServico.Entregue);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StatusNoBanco(ordem.Id)).Should().Be(StatusOrdemDeServico.EmExecucao);
    }
}
