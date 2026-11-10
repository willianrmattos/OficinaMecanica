using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Execucao;
using OficinaMecanica.Infrastructure.Tests.Support;
using OficinaMecanica.Tests.Common.Fakes;

namespace OficinaMecanica.Infrastructure.Tests.Execucao;

// O cliente da Execucao de verdade (opcoes, repasse de credenciais e pipeline de resiliencia, montados por AddClienteDaExecucao)
// com a rede trocada por um ManipuladorHttpFalso. Os padroes aqui sao rapidos (atraso de retry de 1 ms) e o circuito nao abre
// sozinho; o teste que quer o circuito aberto passa os valores dele.
internal sealed class AmbienteDaExecucao : IDisposable
{
    public const string BaseUrl = "http://execucao.teste";

    private readonly ServiceProvider _provedor;

    private AmbienteDaExecucao(ServiceProvider provedor, ManipuladorHttpFalso manipulador, LoggerCapturador logs)
    {
        _provedor = provedor;
        Manipulador = manipulador;
        Logs = logs;
    }

    public ManipuladorHttpFalso Manipulador { get; }
    public LoggerCapturador Logs { get; }
    public IExecucaoClient Cliente => _provedor.GetRequiredService<IExecucaoClient>();
    public IServiceProvider Servicos => _provedor;

    // Cliente HTTP nomeado com o MESMO pipeline do tipado, para exercitar metodos que o cliente tipado nao usa (ex.: POST).
    public HttpClient HttpComOPipelineDaExecucao() => _provedor.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IExecucaoClient));

    public static AmbienteDaExecucao Criar(
        Action<IServiceCollection>? ajustar = null,
        HttpContext? requisicaoDoChamador = null,
        params (string Chave, string? Valor)[] configuracao)
    {
        var valores = new List<(string, string?)>
        {
            ("Execucao:BaseUrl", BaseUrl),
            ("Execucao:TimeoutSegundos", "5"),
            ("Execucao:TimeoutPorTentativaSegundos", "2"),
            ("Execucao:TentativasDeRetry", "2"),
            ("Execucao:AtrasoDoRetryMs", "1"),
            ("Execucao:CircuitoMinimoDeChamadas", "1000"),
            ("Execucao:CircuitoTaxaDeFalha", "0.5"),
            ("Execucao:CircuitoJanelaSegundos", "30"),
            ("Execucao:CircuitoPausaSegundos", "30")
        };
        valores.AddRange(configuracao);

        var manipulador = new ManipuladorHttpFalso();
        var logs = new LoggerCapturador();

        var services = new ServiceCollection();
        services.AddLogging(l =>
        {
            l.ClearProviders();
            l.SetMinimumLevel(LogLevel.Warning);
            l.AddProvider(logs);
        });

        var acessor = new HttpContextAccessor { HttpContext = requisicaoDoChamador };
        services.AddSingleton<IHttpContextAccessor>(acessor);

        services.AddClienteDaExecucao(ConfiguracaoDeTeste.Criar(valores.ToArray()));

        // A rede e substituida; o RepasseDeCredenciaisHandler e o pipeline de resiliencia continuam no caminho.
        services.Configure<HttpClientFactoryOptions>(
            nameof(IExecucaoClient),
            opcoes => opcoes.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = manipulador));

        ajustar?.Invoke(services);

        return new AmbienteDaExecucao(services.BuildServiceProvider(validateScopes: true), manipulador, logs);
    }

    public static HttpContext RequisicaoComToken(string token = "token-do-chamador")
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers.Authorization = $"Bearer {token}";
        return contexto;
    }

    public void Dispose() => _provedor.Dispose();

    public static string JsonDaOrdemDeProducao(Guid id) => $$"""
        {
          "id": "{{id}}",
          "numero": "OS-2026-0001",
          "veiculo": { "placa": "ABC1D23", "marca": "Honda", "modelo": "Civic" },
          "observacoes": "Barulho ao frear.",
          "status": "EmExecucao",
          "mecanicoId": "mecanico-1",
          "servicos": [ { "servicoId": "{{Guid.NewGuid()}}", "nome": "Troca de pastilhas", "quantidade": 1, "precoUnitario": 150.0 } ],
          "pecas": [ { "pecaId": "{{Guid.NewGuid()}}", "nome": "Pastilha", "quantidade": 2, "precoUnitario": 40.0 } ],
          "etapas": [
            { "nome": "Diagnostico", "mecanicoId": "mecanico-1", "status": "Concluida", "inicio": "2026-03-10T12:00:00+00:00", "fim": "2026-03-10T13:00:00+00:00" },
            { "nome": "Execucao", "mecanicoId": "mecanico-1", "status": "EmAndamento", "inicio": "2026-03-10T14:00:00+00:00", "fim": null }
          ],
          "apontamentos": [ { "mecanicoId": "mecanico-1", "texto": "Pastilha dianteira trocada.", "em": "2026-03-10T14:30:00+00:00" } ],
          "laudo": { "texto": "Pastilhas gastas.", "horasEstimadas": 2.5 },
          "resumo": null,
          "motivoInviabilidade": null,
          "inviabilizadaPor": null,
          "motivoCancelamento": null,
          "enfileiradaEm": "2026-03-10T11:00:00+00:00",
          "liberadaEm": "2026-03-10T11:30:00+00:00"
        }
        """;
}
