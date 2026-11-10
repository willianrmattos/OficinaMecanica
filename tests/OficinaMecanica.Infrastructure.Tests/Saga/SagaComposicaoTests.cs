using System.Text.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Saga;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OficinaMecanica.Application;
using OficinaMecanica.Contracts;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// A saga entra pelos ganchos de AddMensageria: mesmo bus, mesmo retry, mesmo outbox, e a guarda de ambiente continua valendo.
public class SagaComposicaoTests
{
    private static ServiceProvider Montar(string ambiente, params (string Chave, string? Valor)[] configuracao)
    {
        var cfg = ConfiguracaoDeTeste.Criar(configuracao);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new AmbienteFalso(ambiente));
        services.AddApplication();
        services.AddInfrastructure(cfg);
        services.AddMensageriaComSaga(cfg);
        return services.BuildServiceProvider();
    }

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

    // O endpoint de saga despacha cada tipo de mensagem por um pipe proprio; leio os filtros do pipe de OrdemDeServicoAberta.
    private static List<string?> FiltrosDoConsumoDaSaga(ServiceProvider provedor)
    {
        var json = JsonSerializer.SerializeToDocument(provedor.GetRequiredService<IBusControl>().GetProbeResult());
        var endpoint = Achar(json.RootElement, e => e.ValueKind == JsonValueKind.Object
                && e.TryGetProperty("name", out var nome) && nome.GetString() == Filas.Saga && e.TryGetProperty("filters", out _))
            ?? throw new InvalidOperationException($"O probe do bus nao descreve o endpoint {Filas.Saga}.");
        var pipe = Achar(endpoint, e => e.ValueKind == JsonValueKind.Object
                && e.TryGetProperty("filterType", out var tipo) && tipo.GetString() == "dispatchPipe"
                && e.TryGetProperty("outputType", out var saida) && (saida.GetString() ?? "").Contains("OrdemDeServicoAberta"))
            ?? throw new InvalidOperationException("O endpoint da saga nao despacha OrdemDeServicoAberta.");

        return pipe.GetProperty("filters").EnumerateArray().Select(f => f.GetProperty("filterType").GetString()).ToList();
    }

    [Fact]
    public async Task AddMensageriaComSaga_ComOutboxDoEf_DeveCriarOEndpointOsSagaComRetryPorForaEOutboxPorDentro()
    {
        await using var provedor = Montar("Testing", ("Mensageria:Persistencia", "EntityFramework"));

        FiltrosDoConsumoDaSaga(provedor).Should().Equal("retry", "outbox", "correlationId", "saga");
    }

    [Fact]
    public async Task AddMensageriaComSaga_SemPersistencia_DeveCriarOEndpointSoComRetry()
    {
        await using var provedor = Montar("Testing", ("Mensageria:Persistencia", "Nenhuma"));

        FiltrosDoConsumoDaSaga(provedor).Should().Equal("retry", "correlationId", "saga");
    }

    [Fact]
    public async Task AddMensageriaComSaga_ComOutboxDoEf_DeveUsarORepositorioDeSagaDoEntityFramework()
    {
        await using var provedor = Montar("Testing", ("Mensageria:Persistencia", "EntityFramework"));
        using var escopo = provedor.CreateScope();

        escopo.ServiceProvider.GetRequiredService<ISagaRepositoryContextFactory<OrdemDeServicoSagaState>>().GetType().Name
            .Should().StartWith("EntityFrameworkSagaRepositoryContextFactory");
    }

    [Fact]
    public async Task AddMensageriaComSaga_SemPersistencia_DeveUsarORepositorioDeSagaEmMemoria()
    {
        await using var provedor = Montar("Testing", ("Mensageria:Persistencia", "Nenhuma"));
        using var escopo = provedor.CreateScope();

        escopo.ServiceProvider.GetRequiredService<ISagaRepositoryContextFactory<OrdemDeServicoSagaState>>().GetType().Name
            .Should().StartWith("InMemorySagaRepositoryContextFactory");
    }

    [Fact]
    public void AddMensageriaComSaga_ComPersistenciaInvalida_DeveFalharEmQualquerAmbienteSemCairEmMemoria()
    {
        var montar = () => Montar("Production", ("Mensageria:Persistencia", "9"));

        montar.Should().Throw<InvalidOperationException>().WithMessage("*Persistencia*");
    }

    [Fact]
    public async Task AddMensageriaComSaga_EmProducaoComPersistenciaNenhuma_DeveDerrubarAPartidaComAGuardaDeAmbiente()
    {
        var cfg = ConfiguracaoDeTeste.Criar(("Mensageria:Transporte", "RabbitMq"), ("Mensageria:Persistencia", "Nenhuma"));
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = "Production" });
        builder.Configuration.AddConfiguration(cfg);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddMensageriaComSaga(builder.Configuration);
        using var host = builder.Build();

        var partida = async () => await host.StartAsync();

        (await partida.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain("Persistencia");
    }
}
