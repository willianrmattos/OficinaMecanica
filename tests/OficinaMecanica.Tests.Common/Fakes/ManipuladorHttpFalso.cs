using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace OficinaMecanica.Tests.Common.Fakes;

// HttpMessageHandler programavel para testar clientes HTTP sem rede: cada chamada consome a proxima resposta do roteiro (ou a
// resposta padrao, quando o roteiro acaba) e fica registrada com os cabecalhos que o cliente enviou. Sem roteiro nem padrao, a
// chamada reprova com mensagem clara: uma chamada a mais do que o teste esperava e um achado, nao um 200 silencioso.
public sealed class ManipuladorHttpFalso : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _roteiro = new();
    private readonly ConcurrentQueue<RequisicaoRegistrada> _requisicoes = new();
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? _padrao;

    public IReadOnlyList<RequisicaoRegistrada> Requisicoes => _requisicoes.ToArray();

    public int Chamadas => _requisicoes.Count;

    // Proxima chamada responde com este status e, se houver, este corpo JSON.
    public ManipuladorHttpFalso Responde(HttpStatusCode status, string? json = null)
    {
        _roteiro.Enqueue((_, _) => Task.FromResult(Resposta(status, json)));
        return this;
    }

    // Proxima chamada lanca esta excecao (ex.: HttpRequestException de rede).
    public ManipuladorHttpFalso Lanca(Exception excecao)
    {
        _roteiro.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(excecao));
        return this;
    }

    // Proxima chamada nunca responde: so termina quando quem chamou cancela (o timeout do pipeline de resiliencia).
    public ManipuladorHttpFalso NuncaResponde()
    {
        _roteiro.Enqueue(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Resposta(HttpStatusCode.OK, null);
        });
        return this;
    }

    // Resposta de toda chamada depois de o roteiro acabar.
    public ManipuladorHttpFalso SempreResponde(HttpStatusCode status, string? json = null)
    {
        _padrao = (_, _) => Task.FromResult(Resposta(status, json));
        return this;
    }

    public ManipuladorHttpFalso SempreLanca(Exception excecao)
    {
        _padrao = (_, _) => Task.FromException<HttpResponseMessage>(excecao);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requisicoes.Enqueue(RequisicaoRegistrada.De(request));

        if (_roteiro.TryDequeue(out var passo))
            return passo(request, cancellationToken);

        return _padrao is null
            ? throw new InvalidOperationException($"O ManipuladorHttpFalso recebeu a chamada {_requisicoes.Count} ({request.Method} {request.RequestUri}) sem resposta programada.")
            : _padrao(request, cancellationToken);
    }

    private static HttpResponseMessage Resposta(HttpStatusCode status, string? json) => new(status)
    {
        Content = json is null ? new StringContent(string.Empty) : new StringContent(json, Encoding.UTF8, "application/json")
    };
}

public sealed record RequisicaoRegistrada(
    HttpMethod Metodo, Uri? Uri, string? Authorization, string? Correlacao, IReadOnlyDictionary<string, string[]> Cabecalhos)
{
    internal static RequisicaoRegistrada De(HttpRequestMessage request) => new(
        request.Method,
        request.RequestUri,
        request.Headers.Authorization?.ToString(),
        request.Headers.TryGetValues("X-Correlation-Id", out var correlacao) ? correlacao.FirstOrDefault() : null,
        request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray()));
}
