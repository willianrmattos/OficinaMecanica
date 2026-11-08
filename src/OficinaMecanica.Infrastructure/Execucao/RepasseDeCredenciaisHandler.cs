using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace OficinaMecanica.Infrastructure.Execucao;

// Repassa a Execucao a identidade e a correlacao da requisicao que o OS Service esta atendendo: o JWT do chamador (a Execucao
// valida o mesmo token, emitido pelo Seguranca, e decide por conta propria se ele pode ler) e um id de correlacao. O contexto
// de trace (traceparent) o proprio HttpClient propaga quando ha um Activity corrente. O token nunca e logado nem guardado: so
// copiado para a requisicao de saida, e so para o BaseUrl configurado, que e o unico destino deste cliente.
public sealed class RepasseDeCredenciaisHandler : DelegatingHandler
{
    public const string CabecalhoDeCorrelacao = "X-Correlation-Id";

    private readonly IHttpContextAccessor _contexto;

    public RepasseDeCredenciaisHandler(IHttpContextAccessor contexto)
    {
        _contexto = contexto;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var http = _contexto.HttpContext;

        if (http is not null)
        {
            if (request.Headers.Authorization is null
                && AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization.ToString(), out var autorizacao))
            {
                request.Headers.Authorization = autorizacao;
            }

            if (!request.Headers.Contains(CabecalhoDeCorrelacao))
                request.Headers.TryAddWithoutValidation(CabecalhoDeCorrelacao, Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
