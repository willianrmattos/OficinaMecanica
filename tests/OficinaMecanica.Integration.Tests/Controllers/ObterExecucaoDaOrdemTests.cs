using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Controllers;

// GET /api/ordens-de-servico/{id}/execucao: o OS Service le a OS e a saga do proprio banco e pergunta a Execucao (cliente HTTP
// real, com repasse do JWT, retry e circuit breaker). Com a Execucao fora ou sem a ordem, a resposta continua sendo 200 com o
// ultimo estado conhecido da saga.
public class ObterExecucaoDaOrdemTests : IClassFixture<FabricaComExecucaoSimulada>
{
    private static readonly DateTimeOffset SagaAtualizadaEm = new(2026, 3, 10, 15, 30, 0, TimeSpan.Zero);

    private readonly FabricaComExecucaoSimulada _fabrica;
    private readonly HttpClient _client;

    public ObterExecucaoDaOrdemTests(FabricaComExecucaoSimulada fabrica)
    {
        _fabrica = fabrica;
        _client = fabrica.CreateClient();
    }

    private string Autenticar()
    {
        var token = TestTokenFactory.GerarToken(_fabrica);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token;
    }

    private async Task<(OficinaMecanica.Domain.Entities.OrdemDeServico Ordem, string EstadoDaSaga)> OrdemAguardandoPagamentoNaSagaAsync()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        var saga = new OrdemDeServicoSagaState
        {
            CorrelationId = ordem.Id,
            CurrentState = "AguardandoPagamento",
            Numero = ordem.Numero,
            CriadaEm = SagaAtualizadaEm.AddDays(-1),
            AtualizadaEm = SagaAtualizadaEm
        };
        await _fabrica.SemearAsync(ordem, saga);
        return (ordem, saga.CurrentState);
    }

    private static string JsonDaProducao(Guid id) => $$"""
        {
          "id": "{{id}}", "numero": "X", "veiculo": { "placa": "ABC1D23", "marca": "Honda", "modelo": "Civic" },
          "status": "EmExecucao", "mecanicoId": "mecanico-1", "servicos": [], "pecas": [],
          "etapas": [ { "nome": "Execucao", "mecanicoId": "mecanico-1", "status": "EmAndamento", "inicio": "2026-03-10T14:00:00+00:00", "fim": null } ],
          "apontamentos": [ { "mecanicoId": "mecanico-1", "texto": "Peça trocada.", "em": "2026-03-10T14:30:00+00:00" } ],
          "laudo": { "texto": "Pastilhas gastas.", "horasEstimadas": 2.5 },
          "resumo": null, "motivoInviabilidade": null, "inviabilizadaPor": null, "motivoCancelamento": null,
          "enfileiradaEm": "2026-03-10T11:00:00+00:00", "liberadaEm": null
        }
        """;

    [Fact]
    public async Task Obter_SemToken_DeveRetornar401SemChamarAExecucao()
    {
        var antes = _fabrica.Execucao.Chamadas;

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{Guid.NewGuid()}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _fabrica.Execucao.Chamadas.Should().Be(antes);
    }

    [Fact]
    public async Task Obter_OrdemInexistente_DeveRetornar404SemChamarAExecucao()
    {
        Autenticar();
        var antes = _fabrica.Execucao.Chamadas;

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{Guid.NewGuid()}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _fabrica.Execucao.Chamadas.Should().Be(antes);
    }

    [Fact]
    public async Task Obter_IdVazio_DeveRetornar400()
    {
        Autenticar();

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{Guid.Empty}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Obter_ExecucaoResponde_DeveDevolverODetalheDaProducaoERepassarOTokenDoChamador()
    {
        var token = Autenticar();
        var (ordem, _) = await OrdemAguardandoPagamentoNaSagaAsync();
        var antes = _fabrica.Execucao.Chamadas;
        _fabrica.Execucao.Responde(HttpStatusCode.OK, JsonDaProducao(ordem.Id));

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("origem").GetString().Should().Be("execucao");
        corpo.GetProperty("ordemDeServicoId").GetGuid().Should().Be(ordem.Id);
        corpo.GetProperty("numero").GetString().Should().Be(ordem.Numero);
        corpo.GetProperty("statusDaOrdem").GetString().Should().Be("AguardandoPagamento");
        corpo.GetProperty("estadoDaSaga").GetString().Should().Be("AguardandoPagamento");
        corpo.GetProperty("motivoDoFallback").ValueKind.Should().Be(JsonValueKind.Null);
        var producao = corpo.GetProperty("producao");
        producao.GetProperty("status").GetString().Should().Be("EmExecucao");
        producao.GetProperty("mecanicoId").GetString().Should().Be("mecanico-1");
        producao.GetProperty("laudo").GetProperty("texto").GetString().Should().Be("Pastilhas gastas.");
        producao.GetProperty("etapas").GetArrayLength().Should().Be(1);
        producao.GetProperty("apontamentos")[0].GetProperty("texto").GetString().Should().Be("Peça trocada.");
        producao.TryGetProperty("veiculo", out _).Should().BeFalse("o OS Service nao repassa o que a Execucao devolve alem do que decidiu expor");

        var enviada = _fabrica.Execucao.Requisicoes.Skip(antes).Single();
        enviada.Metodo.Should().Be(HttpMethod.Get);
        enviada.Uri!.AbsolutePath.Should().Be($"/api/ordens-de-producao/{ordem.Id}");
        enviada.Authorization.Should().Be($"Bearer {token}", "a Execucao valida o mesmo JWT do chamador");
        enviada.Correlacao.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Obter_ExecucaoRespondeComOutraOrdem_DeveDevolver200ComOEstadoDaSagaSemOsDadosDaOutraOs()
    {
        Autenticar();
        var (ordem, estado) = await OrdemAguardandoPagamentoNaSagaAsync();
        var antes = _fabrica.Execucao.Chamadas;
        _fabrica.Execucao.Responde(HttpStatusCode.OK, JsonDaProducao(Guid.NewGuid()));

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("origem").GetString().Should().Be("saga");
        corpo.GetProperty("motivoDoFallback").GetString().Should().Be("ExecucaoIndisponivel");
        corpo.GetProperty("ordemDeServicoId").GetGuid().Should().Be(ordem.Id);
        corpo.GetProperty("estadoDaSaga").GetString().Should().Be(estado);
        corpo.GetProperty("producao").ValueKind.Should().Be(JsonValueKind.Null, "a producao de outra OS nao pode aparecer sob esta");
        _fabrica.Execucao.Chamadas.Should().Be(antes + 1, "resposta de outra ordem nao e repetida");
    }

    [Fact]
    public async Task Obter_Execucao404_DeveDevolver200ComOEstadoDaSagaEMotivoDeOrdemNaoEnfileirada()
    {
        Autenticar();
        var (ordem, estado) = await OrdemAguardandoPagamentoNaSagaAsync();
        _fabrica.Execucao.Responde(HttpStatusCode.NotFound);

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("origem").GetString().Should().Be("saga");
        corpo.GetProperty("motivoDoFallback").GetString().Should().Be("ProducaoNaoEnfileirada");
        corpo.GetProperty("estadoDaSaga").GetString().Should().Be(estado);
        corpo.GetProperty("estadoDaSagaAtualizadoEm").GetDateTimeOffset().Should().Be(SagaAtualizadaEm);
        corpo.GetProperty("statusDaOrdem").GetString().Should().Be("AguardandoPagamento");
        corpo.GetProperty("producao").ValueKind.Should().Be(JsonValueKind.Null, "a saga nao tem dados da producao");
    }

    [Fact]
    public async Task Obter_Execucao5xx_DeveRepetirUmaVezEDevolver200ComOEstadoDaSaga()
    {
        Autenticar();
        var (ordem, estado) = await OrdemAguardandoPagamentoNaSagaAsync();
        var antes = _fabrica.Execucao.Chamadas;
        _fabrica.Execucao.Responde(HttpStatusCode.ServiceUnavailable).Responde(HttpStatusCode.BadGateway);

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("origem").GetString().Should().Be("saga");
        corpo.GetProperty("motivoDoFallback").GetString().Should().Be("ExecucaoIndisponivel");
        corpo.GetProperty("estadoDaSaga").GetString().Should().Be(estado);
        _fabrica.Execucao.Chamadas.Should().Be(antes + 2, "uma tentativa e um retry");
    }

    [Fact]
    public async Task Obter_Execucao503DepoisOk_DeveRecuperarNoRetryEDevolverOrigemExecucao()
    {
        Autenticar();
        var (ordem, _) = await OrdemAguardandoPagamentoNaSagaAsync();
        _fabrica.Execucao.Responde(HttpStatusCode.ServiceUnavailable).Responde(HttpStatusCode.OK, JsonDaProducao(ordem.Id));

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");

        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("origem").GetString().Should().Be("execucao");
    }

    [Fact]
    public async Task Obter_Execucao401_DeveDevolver200ComEstadoDaSagaSemRepetir()
    {
        Autenticar();
        var (ordem, _) = await OrdemAguardandoPagamentoNaSagaAsync();
        var antes = _fabrica.Execucao.Chamadas;
        _fabrica.Execucao.Responde(HttpStatusCode.Unauthorized);

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("origem").GetString().Should().Be("saga");
        _fabrica.Execucao.Chamadas.Should().Be(antes + 1, "4xx nunca e repetido");
    }

    [Fact]
    public async Task Obter_ExecucaoNaoResponde_DeveEstourarOTimeoutEDevolver200ComOEstadoDaSaga()
    {
        Autenticar();
        var (ordem, estado) = await OrdemAguardandoPagamentoNaSagaAsync();
        _fabrica.Execucao.NuncaResponde().NuncaResponde();

        var inicio = DateTime.UtcNow;
        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("origem").GetString().Should().Be("saga");
        corpo.GetProperty("estadoDaSaga").GetString().Should().Be(estado);
        (DateTime.UtcNow - inicio).Should().BeLessThan(TimeSpan.FromSeconds(5), "o timeout total (2 s) limita a espera do usuario");
    }

    [Fact]
    public async Task Obter_OrdemSemLinhaDeSaga_DeveDevolverEstadoDaSagaNulo()
    {
        Autenticar();
        var ordem = new OrdemDeServicoBuilder().AnteriorASaga().Recebida();
        await _fabrica.SemearAsync(ordem);
        _fabrica.Execucao.Responde(HttpStatusCode.NotFound);

        var resposta = await _client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("estadoDaSaga").ValueKind.Should().Be(JsonValueKind.Null);
        corpo.GetProperty("estadoDaSagaAtualizadoEm").ValueKind.Should().Be(JsonValueKind.Null);
        corpo.GetProperty("statusDaOrdem").GetString().Should().Be("Recebida");
    }

    // Fabrica propria, com o circuito de duas chamadas: o circuito e da fabrica inteira e abriria o dos demais testes.
    [Fact]
    public async Task Obter_FalhasSeguidas_DeveAbrirOCircuitoEContinuarRespondendo200SemChamarAExecucao()
    {
        await using var fabrica = new FabricaComExecucaoSimulada(minimoDeChamadasDoCircuito: 2);
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenFactory.GerarToken(fabrica));
        var ordem = new OrdemDeServicoBuilder().AguardandoPagamento();
        await fabrica.SemearAsync(ordem);
        fabrica.Execucao.SempreResponde(HttpStatusCode.ServiceUnavailable);

        var primeira = await client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");
        var chamadasAteAqui = fabrica.Execucao.Chamadas;
        var segunda = await client.GetAsync($"/api/ordens-de-servico/{ordem.Id}/execucao");

        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        segunda.StatusCode.Should().Be(HttpStatusCode.OK);
        (await segunda.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("origem").GetString().Should().Be("saga");
        chamadasAteAqui.Should().Be(2);
        fabrica.Execucao.Chamadas.Should().Be(2, "com o circuito aberto a Execucao nem e chamada");
    }
}
