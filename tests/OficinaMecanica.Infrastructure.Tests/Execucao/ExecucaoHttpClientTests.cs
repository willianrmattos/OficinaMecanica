using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Execucao;
using OficinaMecanica.Tests.Common.Fakes;

namespace OficinaMecanica.Infrastructure.Tests.Execucao;

// O cliente da consulta sincrona a Execucao com o pipeline de resiliencia de verdade (timeout total, retry, circuit breaker,
// timeout por tentativa) e a rede trocada por um manipulador falso: cada teste diz o que a Execucao responde e afirma o que o
// OS Service concluiu e quantas vezes tentou.
public class ExecucaoHttpClientTests
{
    private static readonly Guid OrdemId = Guid.Parse("7f1c2b3a-0d4e-4f5a-8b6c-9d0e1f2a3b4c");

    // ---- Sucesso e traducao do contrato ----

    [Fact]
    public async Task Obter_Execucao200_DeveTraduzirOContratoDaExecucaoParaOModeloProprioIgnorandoOResto()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.Responde(HttpStatusCode.OK, AmbienteDaExecucao.JsonDaOrdemDeProducao(OrdemId));

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Encontrada);
        var producao = resultado.Producao!;
        producao.Status.Should().Be("EmExecucao");
        producao.MecanicoId.Should().Be("mecanico-1");
        producao.Laudo.Should().BeEquivalentTo(new { Texto = "Pastilhas gastas.", HorasEstimadas = 2.5m });
        producao.Resumo.Should().BeNull();
        producao.EnfileiradaEm.Should().Be(new DateTimeOffset(2026, 3, 10, 11, 0, 0, TimeSpan.Zero));
        producao.LiberadaEm.Should().Be(new DateTimeOffset(2026, 3, 10, 11, 30, 0, TimeSpan.Zero));
        producao.Etapas.Select(e => (e.Nome, e.Status)).Should().Equal(("Diagnostico", "Concluida"), ("Execucao", "EmAndamento"));
        producao.Etapas[1].Fim.Should().BeNull();
        producao.Apontamentos.Should().ContainSingle().Which.Texto.Should().Be("Pastilha dianteira trocada.");
        ambiente.Manipulador.Chamadas.Should().Be(1);
    }

    [Fact]
    public async Task Obter_CamposOpcionaisAusentes_DeveDevolverListasVaziasELaudoNulo()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.Responde(HttpStatusCode.OK, $$"""{ "id": "{{OrdemId}}", "status": "Fila", "enfileiradaEm": "2026-03-10T11:00:00+00:00" }""");

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Encontrada);
        resultado.Producao!.Laudo.Should().BeNull();
        resultado.Producao.Etapas.Should().BeEmpty();
        resultado.Producao.Apontamentos.Should().BeEmpty();
        resultado.Producao.MecanicoId.Should().BeNull();
    }

    [Theory]
    [InlineData("http://execucao.teste", "http://execucao.teste/api/ordens-de-producao/")]
    [InlineData("http://execucao.teste/", "http://execucao.teste/api/ordens-de-producao/")]
    [InlineData("http://cluster.local/execucao", "http://cluster.local/execucao/api/ordens-de-producao/")]
    public async Task Obter_DeveChamarAhRotaRealDaExecucaoSobOBaseUrlConfigurado(string baseUrl, string prefixoEsperado)
    {
        using var ambiente = AmbienteDaExecucao.Criar(configuracao: ("Execucao:BaseUrl", baseUrl));
        ambiente.Manipulador.Responde(HttpStatusCode.NotFound);

        await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        var requisicao = ambiente.Manipulador.Requisicoes.Single();
        requisicao.Metodo.Should().Be(HttpMethod.Get);
        requisicao.Uri!.ToString().Should().Be(prefixoEsperado + OrdemId);
    }

    [Fact]
    public async Task Obter_RespostaDeOutraOrdem_DeveSerIndisponivelSemExporOsDadosELogarSoOsDoisIds()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        var outraOrdem = Guid.NewGuid();
        ambiente.Manipulador.Responde(HttpStatusCode.OK, AmbienteDaExecucao.JsonDaOrdemDeProducao(outraOrdem));

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        resultado.Detalhe.Should().Be("resposta de outra ordem");
        resultado.Producao.Should().BeNull("os dados de outra OS nunca podem aparecer na consulta desta");
        ambiente.Manipulador.Chamadas.Should().Be(1, "resposta errada nao e falha transitoria: nao se repete");
        var aviso = ambiente.Logs.Registros.Should().ContainSingle(r => r.Nivel == LogLevel.Warning).Subject;
        aviso.Mensagem.Should().Contain(OrdemId.ToString()).And.Contain(outraOrdem.ToString());
        aviso.Mensagem.Should().NotContain("mecanico-1").And.NotContain("Pastilhas gastas");
    }

    [Fact]
    public async Task Obter_RespostaSemId_DeveSerTratadaComoDeOutraOrdem()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.Responde(HttpStatusCode.OK, "{ \"status\": \"Fila\", \"enfileiradaEm\": \"2026-03-10T11:00:00+00:00\" }");

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        resultado.Detalhe.Should().Be("resposta de outra ordem");
    }

    // ---- 404 e 4xx: nunca se repete ----

    [Fact]
    public async Task Obter_Execucao404_DeveDizerQueAOrdemAindaNaoFoiEnfileiradaSemRepetirNemAvisar()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.Responde(HttpStatusCode.NotFound);

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.NaoEncontrada);
        ambiente.Manipulador.Chamadas.Should().Be(1, "404 nao muda se a chamada for repetida");
        ambiente.Logs.Registros.Should().BeEmpty("a ordem ainda nao enfileirada e um caso normal, nao um aviso");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Obter_QualquerOutro4xx_DeveSerIndisponivelSemNenhumaRepeticao(HttpStatusCode status)
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.Responde(status);

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        resultado.Detalhe.Should().Be($"HTTP {(int)status}");
        ambiente.Manipulador.Chamadas.Should().Be(1, "4xx e erro do chamador: repetir nao resolve (nem 408 nem 429, que o padrao do Polly repetiria)");
    }

    // ---- 5xx e falhas de rede: retry e depois fallback ----

    [Fact]
    public async Task Obter_Execucao503DepoisOk_DeveRepetirEDevolverOResultadoDaSegundaTentativa()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.Responde(HttpStatusCode.ServiceUnavailable).Responde(HttpStatusCode.OK, AmbienteDaExecucao.JsonDaOrdemDeProducao(OrdemId));

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Encontrada);
        ambiente.Manipulador.Chamadas.Should().Be(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Obter_5xxEmTodasAsTentativas_DeveEsgotarOsRetriesEFicarIndisponivel(HttpStatusCode status)
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.SempreResponde(status);

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        resultado.Detalhe.Should().Be($"HTTP {(int)status}");
        ambiente.Manipulador.Chamadas.Should().Be(3, "a primeira tentativa mais os dois retries configurados");
        ambiente.Logs.Registros.Should().ContainSingle(r => r.Nivel == LogLevel.Warning && r.Mensagem.Contains(OrdemId.ToString()));
    }

    [Fact]
    public async Task Obter_FalhaDeRede_DeveRepetirEFicarIndisponivelSemVazarExcecao()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.SempreLanca(new HttpRequestException("Connection refused"));

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        resultado.Detalhe.Should().StartWith("falha de rede");
        ambiente.Manipulador.Chamadas.Should().Be(3);
    }

    [Fact]
    public async Task Obter_RedeCaiDepoisVolta_DeveRecuperarNoRetry()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.Lanca(new HttpRequestException("reset")).Responde(HttpStatusCode.OK, AmbienteDaExecucao.JsonDaOrdemDeProducao(OrdemId));

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Encontrada);
        ambiente.Manipulador.Chamadas.Should().Be(2);
    }

    [Fact]
    public async Task Obter_RespostaOkIlegivel_DeveSerIndisponivelSemRepetir()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.Responde(HttpStatusCode.OK, "{ isto nao e json");

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        resultado.Detalhe.Should().Be("resposta ilegível");
        ambiente.Manipulador.Chamadas.Should().Be(1);
    }

    [Fact]
    public async Task Obter_RespostaOkComCorpoNulo_DeveSerIndisponivel()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.Responde(HttpStatusCode.OK, "null");

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        resultado.Detalhe.Should().Be("resposta vazia");
    }

    // ---- Timeout ----

    [Fact]
    public async Task Obter_ExecucaoNaoResponde_DeveEstourarOTimeoutDeTentativaERepetirAteOTotal()
    {
        using var ambiente = AmbienteDaExecucao.Criar(configuracao: new (string, string?)[]
        {
            ("Execucao:TimeoutSegundos", "5"), ("Execucao:TimeoutPorTentativaSegundos", "1"), ("Execucao:TentativasDeRetry", "1")
        });
        ambiente.Manipulador.SempreLanca(new InvalidOperationException("nao deve ser usada")).NuncaResponde().NuncaResponde();

        var inicio = DateTime.UtcNow;
        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        resultado.Detalhe.Should().Be("timeout");
        ambiente.Manipulador.Chamadas.Should().Be(2, "uma tentativa e um retry, cada um limitado a 1 s");
        (DateTime.UtcNow - inicio).Should().BeLessThan(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task Obter_TimeoutTotalMenorQueAsTentativas_DeveCortarNoTotal()
    {
        using var ambiente = AmbienteDaExecucao.Criar(configuracao: new (string, string?)[]
        {
            ("Execucao:TimeoutSegundos", "1"), ("Execucao:TimeoutPorTentativaSegundos", "1"), ("Execucao:TentativasDeRetry", "5")
        });
        ambiente.Manipulador.SempreLanca(new InvalidOperationException("nao deve ser usada")).NuncaResponde().NuncaResponde().NuncaResponde();

        var inicio = DateTime.UtcNow;
        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        resultado.Detalhe.Should().Be("timeout");
        (DateTime.UtcNow - inicio).Should().BeLessThan(TimeSpan.FromSeconds(3), "o total manda: nao ha 5 tentativas de 1 s");
    }

    [Fact]
    public async Task Obter_CancelamentoPedidoPeloChamador_DeveLancarEnaoVirarIndisponivel()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.NuncaResponde();
        using var cancelamento = new CancellationTokenSource();
        cancelamento.CancelAfter(TimeSpan.FromMilliseconds(100));

        var act = () => ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId, cancelamento.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- Circuit breaker ----

    private static (string, string?)[] CircuitoQueAbreComDuasFalhas(int pausaSegundos = 30) => new (string, string?)[]
    {
        ("Execucao:CircuitoMinimoDeChamadas", "2"), ("Execucao:CircuitoTaxaDeFalha", "0.5"), ("Execucao:CircuitoJanelaSegundos", "30"),
        ("Execucao:CircuitoPausaSegundos", pausaSegundos.ToString()), ("Execucao:TentativasDeRetry", "1")
    };

    [Fact]
    public async Task Obter_FalhasSeguidas_DeveAbrirOCircuitoEDeixarDeChamarAExecucao()
    {
        using var ambiente = AmbienteDaExecucao.Criar(configuracao: CircuitoQueAbreComDuasFalhas());
        ambiente.Manipulador.SempreResponde(HttpStatusCode.ServiceUnavailable);

        var primeira = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);
        var chamadasAteAqui = ambiente.Manipulador.Chamadas;
        var segunda = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);
        var terceira = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        primeira.Detalhe.Should().Be("HTTP 503");
        chamadasAteAqui.Should().Be(2, "a primeira consulta tentou e repetiu uma vez, e as duas falhas abriram o circuito");
        segunda.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
        segunda.Detalhe.Should().Be("circuito aberto");
        terceira.Detalhe.Should().Be("circuito aberto");
        ambiente.Manipulador.Chamadas.Should().Be(2, "com o circuito aberto a Execucao nem e chamada");
    }

    [Fact]
    public async Task Obter_4xxNaoDeveAbrirOCircuito()
    {
        using var ambiente = AmbienteDaExecucao.Criar(configuracao: CircuitoQueAbreComDuasFalhas());
        ambiente.Manipulador.SempreResponde(HttpStatusCode.Unauthorized);

        for (var i = 0; i < 6; i++)
            (await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId)).Detalhe.Should().Be("HTTP 401");

        ambiente.Manipulador.Chamadas.Should().Be(6, "erro do chamador nao conta para o circuito: a Execucao continua sendo chamada");
    }

    [Fact]
    public async Task Obter_DepoisDaPausa_DeveDeixarUmaChamadaDeTestePassarEFecharOCircuitoSeDerCerto()
    {
        var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));
        using var ambiente = AmbienteDaExecucao.Criar(
            ajustar: servicos => servicos.AddSingleton<TimeProvider>(relogio),
            configuracao: CircuitoQueAbreComDuasFalhas(pausaSegundos: 30));
        ambiente.Manipulador.Responde(HttpStatusCode.ServiceUnavailable).Responde(HttpStatusCode.ServiceUnavailable);

        // O pipeline usa o TimeProvider do container, inclusive para a espera entre tentativas: com o relogio falso parado, o
        // retry nunca acordaria. Por isso a primeira consulta so termina enquanto o teste avanca o relogio aos poucos.
        await AvancandoORelogioAsync(relogio, ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId));
        (await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId)).Detalhe.Should().Be("circuito aberto");

        relogio.Advance(TimeSpan.FromSeconds(31));
        ambiente.Manipulador.Responde(HttpStatusCode.OK, AmbienteDaExecucao.JsonDaOrdemDeProducao(OrdemId));
        var depois = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        depois.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Encontrada, "passada a pausa, a chamada de teste da Execucao recuperada fecha o circuito");
        ambiente.Manipulador.Chamadas.Should().Be(3);
    }

    // Faz o relogio falso andar ate a tarefa terminar, com teto de 10 s de relogio real para nunca esperar sem limite.
    private static async Task<T> AvancandoORelogioAsync<T>(FakeTimeProvider relogio, Task<T> tarefa)
    {
        var limite = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!tarefa.IsCompleted)
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException("A consulta não terminou em 10 s mesmo avançando o relógio falso.");

            relogio.Advance(TimeSpan.FromMilliseconds(5));
            await Task.Delay(1);
        }

        return await tarefa;
    }

    // ---- So GET e repetido ----

    [Fact]
    public async Task Pipeline_RequisicaoPost_NaoDeveSerRepetidaMesmoComErro5xx()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.SempreResponde(HttpStatusCode.ServiceUnavailable);
        using var http = ambiente.HttpComOPipelineDaExecucao();

        var resposta = await http.PostAsync("api/qualquer-coisa", new StringContent("{}"));

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        ambiente.Manipulador.Chamadas.Should().Be(1, "so GET e idempotente o bastante para ser repetido");
    }

    [Fact]
    public async Task Pipeline_RequisicaoGet_DeveSerRepetidaNo5xx()
    {
        using var ambiente = AmbienteDaExecucao.Criar();
        ambiente.Manipulador.SempreResponde(HttpStatusCode.ServiceUnavailable);
        using var http = ambiente.HttpComOPipelineDaExecucao();

        var resposta = await http.GetAsync("api/qualquer-coisa");

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        ambiente.Manipulador.Chamadas.Should().Be(3);
    }

    // ---- Repasse do JWT e da correlacao ----

    [Fact]
    public async Task Obter_DeveRepassarOTokenDoChamadoEACorrelacaoNaRequisicaoDeSaida()
    {
        var requisicao = AmbienteDaExecucao.RequisicaoComToken("abc.def.ghi");
        requisicao.TraceIdentifier = "trace-da-requisicao-1";
        using var ambiente = AmbienteDaExecucao.Criar(requisicaoDoChamador: requisicao);
        ambiente.Manipulador.Responde(HttpStatusCode.NotFound);

        await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        var enviada = ambiente.Manipulador.Requisicoes.Single();
        enviada.Authorization.Should().Be("Bearer abc.def.ghi");
        enviada.Correlacao.Should().Be("trace-da-requisicao-1");
    }

    [Fact]
    public async Task Obter_DeveRepassarOTokenEmTodasAsTentativasDoRetry()
    {
        var requisicao = AmbienteDaExecucao.RequisicaoComToken("abc.def.ghi");
        using var ambiente = AmbienteDaExecucao.Criar(requisicaoDoChamador: requisicao);
        ambiente.Manipulador.SempreResponde(HttpStatusCode.BadGateway);

        await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        ambiente.Manipulador.Requisicoes.Should().HaveCount(3).And.OnlyContain(r => r.Authorization == "Bearer abc.def.ghi");
    }

    [Fact]
    public async Task Obter_UsandoOActivityCorrente_DeveCorrelacionarPeloTraceId()
    {
        using var origem = new System.Diagnostics.ActivitySource("teste-execucao");
        using var ouvinte = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = fonte => fonte.Name == "teste-execucao",
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllData
        };
        System.Diagnostics.ActivitySource.AddActivityListener(ouvinte);
        using var ambiente = AmbienteDaExecucao.Criar(requisicaoDoChamador: AmbienteDaExecucao.RequisicaoComToken());
        ambiente.Manipulador.Responde(HttpStatusCode.NotFound);

        using var atividade = origem.StartActivity("consulta");
        await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        ambiente.Manipulador.Requisicoes.Single().Correlacao.Should().Be(atividade!.TraceId.ToString());
    }

    [Fact]
    public async Task Obter_SemRequisicaoEmCurso_NaoDeveInventarCredenciais()
    {
        using var ambiente = AmbienteDaExecucao.Criar(requisicaoDoChamador: null);
        ambiente.Manipulador.Responde(HttpStatusCode.Unauthorized);

        var resultado = await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        var enviada = ambiente.Manipulador.Requisicoes.Single();
        enviada.Authorization.Should().BeNull();
        enviada.Correlacao.Should().BeNull();
        resultado.Situacao.Should().Be(SituacaoDaConsultaAExecucao.Indisponivel);
    }

    [Fact]
    public async Task Obter_ChamadorSemAuthorization_NaoDeveEnviarCabecalhoDeAutorizacao()
    {
        using var ambiente = AmbienteDaExecucao.Criar(requisicaoDoChamador: new Microsoft.AspNetCore.Http.DefaultHttpContext());
        ambiente.Manipulador.Responde(HttpStatusCode.Unauthorized);

        await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        ambiente.Manipulador.Requisicoes.Single().Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Obter_NaoDeveLogarOTokenDoChamador()
    {
        using var ambiente = AmbienteDaExecucao.Criar(requisicaoDoChamador: AmbienteDaExecucao.RequisicaoComToken("segredo-que-nao-pode-vazar"));
        ambiente.Manipulador.SempreResponde(HttpStatusCode.ServiceUnavailable);

        await ambiente.Cliente.ObterOrdemDeProducaoAsync(OrdemId);

        ambiente.Logs.Registros.Should().NotBeEmpty();
        ambiente.Logs.Registros.Should().NotContain(r => r.Mensagem.Contains("segredo-que-nao-pode-vazar") || r.Mensagem.Contains("Bearer"));
    }
}
