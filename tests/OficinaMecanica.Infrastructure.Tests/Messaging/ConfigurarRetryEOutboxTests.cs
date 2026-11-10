using System.Text.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OficinaMecanica.Application;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Messaging;

// A ordem dos filtros do endpoint e lida do probe do bus (a descricao que o proprio MassTransit da do pipeline de
// consumo), sem subir broker nem banco: retry por fora, outbox/inbox por dentro, consumer por ultimo.
public class ConfigurarRetryEOutboxTests
{
    private const string Fila = "fila-de-probe";

    private static async Task<JsonElement> ObterFiltrosDoConsumoAsync(params (string Chave, string? Valor)[] configuracao)
    {
        var cfg = ConfiguracaoDeTeste.Criar(configuracao);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new AmbienteFalso("Testing"));
        services.AddApplication();
        services.AddInfrastructure(cfg);
        services.AddSingleton<RegistroDeConsumo>();
        services.AddMensageria(
            cfg,
            registrarConsumers: x => x.AddConsumer<ConsumerDeMensagemDeTeste>(),
            configurarEndpoints: (contexto, bus) => bus.ReceiveEndpoint(Fila, endpoint =>
            {
                endpoint.ConfigurarRetryEOutbox(contexto);
                endpoint.ConfigureConsumer<ConsumerDeMensagemDeTeste>(contexto);
            }));
        await using var provedor = services.BuildServiceProvider();

        var probe = provedor.GetRequiredService<IBusControl>().GetProbeResult();
        var json = JsonSerializer.SerializeToDocument(probe);

        var endpoint = Achar(json.RootElement, e => e.ValueKind == JsonValueKind.Object
                && e.TryGetProperty("name", out var nome) && nome.GetString() == Fila
                && e.TryGetProperty("filters", out _))
            ?? throw new InvalidOperationException($"O probe do bus nao descreve o endpoint {Fila}.");
        var deserializacao = endpoint.GetProperty("filters").EnumerateArray().Single(f => f.GetProperty("filterType").GetString() == "deserialize");
        return deserializacao.GetProperty("consumePipe").GetProperty("filters").GetProperty("filters").Clone();
    }

    // O probe e uma arvore JSON cujo formato exato e do MassTransit; procuro o no pelo nome do endpoint em vez de fixar o caminho.
    private static JsonElement? Achar(JsonElement no, Func<JsonElement, bool> criterio)
    {
        if (criterio(no))
            return no;

        var filhos = no.ValueKind switch
        {
            JsonValueKind.Object => no.EnumerateObject().Select(p => p.Value),
            JsonValueKind.Array => no.EnumerateArray(),
            _ => Enumerable.Empty<JsonElement>()
        };

        foreach (var filho in filhos)
        {
            if (Achar(filho, criterio) is { } encontrado)
                return encontrado;
        }

        return null;
    }

    // Com um filtro so, o probe descreve um objeto; com varios, um array.
    private static List<JsonElement> Listar(JsonElement filtros) =>
        filtros.ValueKind == JsonValueKind.Array ? filtros.EnumerateArray().ToList() : new List<JsonElement> { filtros };

    [Fact]
    public async Task ConfigurarRetryEOutbox_DeveMontarRetryPorForaEOutboxPorDentro()
    {
        var filtros = await ObterFiltrosDoConsumoAsync(("Mensageria:Persistencia", "EntityFramework"));

        Listar(filtros).Select(f => f.GetProperty("filterType").GetString())
            .Should().Equal("retry", "outbox");
    }

    [Fact]
    public async Task ConfigurarRetryEOutbox_ComRetryConfigurado_DeveUsarOsIntervalosDaConfiguracao()
    {
        var filtros = await ObterFiltrosDoConsumoAsync(
            ("Mensageria:Persistencia", "EntityFramework"),
            ("Mensageria:RetryIntervalosMs:0", "100"),
            ("Mensageria:RetryIntervalosMs:1", "200"));

        var retry = Listar(filtros).First().GetProperty("retry-consumeContext");
        retry.GetProperty("policy").GetString().Should().Be("Interval");
        retry.GetProperty("intervals").EnumerateArray().Select(i => i.GetString())
            .Should().Equal("00:00:00.1000000", "00:00:00.2000000");
    }

    [Fact]
    public async Task ConfigurarRetryEOutbox_SemPersistencia_DeveTerSoOFiltroDeRetry()
    {
        var filtros = await ObterFiltrosDoConsumoAsync(("Mensageria:Persistencia", "Nenhuma"));

        Listar(filtros).Select(f => f.GetProperty("filterType").GetString()).Should().Equal("retry");
    }
}
