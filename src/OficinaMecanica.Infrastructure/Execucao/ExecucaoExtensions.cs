using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using OficinaMecanica.Application.Interfaces;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace OficinaMecanica.Infrastructure.Execucao;

// Composicao do cliente da Execucao: opcoes validadas na partida, repasse do JWT do chamador e o pipeline de resiliencia. Fica
// fora do DependencyInjection.cs de proposito (um arquivo por assunto, como a mensageria e a saga).
public static class ExecucaoExtensions
{
    public const string NomeDoPipeline = "execucao";

    public static IServiceCollection AddClienteDaExecucao(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpcoesDaExecucao>().Bind(configuration.GetSection(OpcoesDaExecucao.Secao)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OpcoesDaExecucao>, ValidadorDasOpcoesDaExecucao>());

        services.AddHttpContextAccessor();
        services.AddTransient<RepasseDeCredenciaisHandler>();

        services
            .AddHttpClient<IExecucaoClient, ExecucaoHttpClient>((provider, http) =>
            {
                var opcoes = provider.GetRequiredService<IOptions<OpcoesDaExecucao>>().Value;
                http.BaseAddress = new Uri(opcoes.BaseUrl.TrimEnd('/') + "/");

                // Quem limita o tempo e o pipeline de resiliencia (que sabe distinguir timeout de cancelamento do usuario): o
                // timeout padrao do HttpClient (100 s) so atrapalharia, entao fica sem limite proprio.
                http.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddHttpMessageHandler<RepasseDeCredenciaisHandler>()
            .AddResilienceHandler(NomeDoPipeline, (pipeline, contexto) =>
            {
                var opcoes = contexto.ServiceProvider.GetRequiredService<IOptions<OpcoesDaExecucao>>().Value;
                ConfigurarPipeline(pipeline, opcoes);
            });

        return services;
    }

    // Da fora para dentro: timeout total > retry > circuit breaker > timeout por tentativa. O breaker fica DENTRO do retry para
    // contar cada tentativa, e o timeout de tentativa dentro do breaker para um timeout contar como falha dele.
    internal static void ConfigurarPipeline(ResiliencePipelineBuilder<HttpResponseMessage> pipeline, OpcoesDaExecucao opcoes)
    {
        pipeline.AddTimeout(TimeSpan.FromSeconds(opcoes.TimeoutSegundos));

        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = opcoes.TentativasDeRetry,
            Delay = TimeSpan.FromMilliseconds(opcoes.AtrasoDoRetryMs),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            // Repetir so o que e seguro repetir: GET e idempotente; um POST repetido poderia duplicar efeito no destino.
            ShouldHandle = args => ValueTask.FromResult(EhGet(args.Context) && EhFalhaTransitoria(args.Outcome))
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = opcoes.CircuitoTaxaDeFalha,
            MinimumThroughput = opcoes.CircuitoMinimoDeChamadas,
            SamplingDuration = TimeSpan.FromSeconds(opcoes.CircuitoJanelaSegundos),
            BreakDuration = TimeSpan.FromSeconds(opcoes.CircuitoPausaSegundos),
            ShouldHandle = args => ValueTask.FromResult(EhFalhaTransitoria(args.Outcome))
        });

        pipeline.AddTimeout(TimeSpan.FromSeconds(opcoes.TimeoutPorTentativaSegundos));
    }

    private static bool EhGet(ResilienceContext contexto) =>
        contexto.GetRequestMessage()?.Method == HttpMethod.Get;

    // Falha transitoria e o que pode passar sozinho: 5xx, falha de rede e timeout. Qualquer 4xx (inclusive 408 e 429, que o padrao
    // do Polly repetiria) nao entra: 401/403 e 404 nao mudam se a chamada for repetida, e o breaker nao deve abrir por erro do
    // chamador.
    private static bool EhFalhaTransitoria(Outcome<HttpResponseMessage> resultado) =>
        resultado.Exception switch
        {
            HttpRequestException or TimeoutRejectedException => true,
            null => resultado.Result is { StatusCode: >= HttpStatusCode.InternalServerError },
            _ => false
        };
}
