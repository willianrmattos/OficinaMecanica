using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Moq;
using OficinaMecanica.API.Middleware;

namespace OficinaMecanica.Integration.Tests.Support;

// Apoio dos testes do ExceptionHandlingMiddleware: contexto sem servidor, endpoint com template de rota, leitura do corpo e
// dos logs registrados num Mock<ILogger>.
public static class MiddlewareDeTeste
{
    public static DefaultHttpContext NovoContexto(CancellationToken requisicaoAbortada = default)
    {
        var contexto = new DefaultHttpContext { RequestAborted = requisicaoAbortada };
        contexto.Response.Body = new MemoryStream();
        return contexto;
    }

    // O roteamento do ASP.NET grava o endpoint no contexto antes de qualquer middleware; aqui faço o mesmo à mão.
    public static void DefinirRota(DefaultHttpContext contexto, string template) =>
        contexto.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask, RoutePatternFactory.Parse(template), 0, EndpointMetadataCollection.Empty, "rota de teste"));

    public static List<(LogLevel Nivel, string Mensagem, Exception? Excecao)> Registros(Mock<ILogger<ExceptionHandlingMiddleware>> logger) =>
        logger.Invocations
            .Where(i => i.Method.Name == nameof(ILogger.Log))
            .Select(i => ((LogLevel)i.Arguments[0], i.Arguments[2].ToString()!, (Exception?)i.Arguments[3]))
            .ToList();

    public static string CorpoEscrito(DefaultHttpContext contexto)
    {
        contexto.Response.Body.Position = 0;
        return new StreamReader(contexto.Response.Body).ReadToEnd();
    }

    // O DefaultHttpContext não implementa Abort; este ciclo de vida o registra e cancela o RequestAborted, como o Kestrel, para
    // os testes afirmarem se o middleware encerrou a conexão.
    public sealed class CicloDeVidaDaRequisicao : IHttpRequestLifetimeFeature
    {
        private readonly CancellationTokenSource _abortada = new();

        public CancellationToken RequestAborted
        {
            get => _abortada.Token;
            set => throw new NotSupportedException("O token só é cancelado por ClienteFechouAConexao ou por Abort.");
        }

        public bool AbortFoiChamado { get; private set; }

        // O cliente fecha a conexão por conta própria: o token cancela sem passar por Abort.
        public void ClienteFechouAConexao() => _abortada.Cancel();

        public void Abort()
        {
            AbortFoiChamado = true;
            _abortada.Cancel();
        }
    }

    // Reproduz o Kestrel com a resposta já enviada: mudar o status ou os cabeçalhos lança InvalidOperationException (o
    // HttpResponseFeature padrão deixaria mudar e esconderia o defeito de um middleware que mexe na resposta).
    public sealed class RespostaJaIniciada : IHttpResponseFeature
    {
        public int StatusCode
        {
            get => StatusCodes.Status200OK;
            set => throw new InvalidOperationException("StatusCode cannot be set because the response has already started.");
        }

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary { IsReadOnly = true };

        public Stream Body { get; set; } = new MemoryStream();

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state) =>
            throw new InvalidOperationException("OnStarting cannot be set because the response has already started.");

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}
