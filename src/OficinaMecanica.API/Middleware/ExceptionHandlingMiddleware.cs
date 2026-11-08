using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentValidation;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private static class TipoErro
    {
        public const string Negocio = "Erro de Negócio";
        public const string Validacao = "Erro de Validação";
        public const string NaoEncontrado = "Não Encontrado";
        public const string Conflito = "Conflito";
        public const string Interno = "Erro Interno";
    }

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        // Quando o cliente fecha a conexão (timeout do chamador, do APIM ou do próprio usuário), o ASP.NET cancela o
        // RequestAborted e cada biblioteca que usa o token reage a seu modo: o HttpClient e o EF com o token já cancelado
        // lançam OperationCanceledException, mas o SqlClient, com um comando em voo, lança SqlException ("Operation cancelled
        // by user") e o SaveChangesAsync ainda a embrulha em DbUpdateException. CancelamentoDaRequisicao reconhece as duas
        // formas. Não há mais para quem responder, então não escrevo corpo e não trato como erro do servidor: um ERR por
        // timeout alheio geraria alerta sem haver defeito. Defino 499 (convenção do nginx para "cliente fechou a
        // requisição") para o log de acesso, a telemetria do OpenTelemetry e as métricas HTTP do Prometheus
        // (UseHttpMetrics fica por fora deste middleware), que leem o status final, não contarem a chamada como sucesso; é
        // inofensivo porque ninguém lê essa resposta. Cancelamento que não vem do RequestAborted (por exemplo, o timeout
        // interno de uma dependência), erro real de SQL com a requisição abortada (deadlock, timeout, chave duplicada) e
        // resposta já iniciada seguem para o tratamento geral abaixo.
        catch (Exception ex) when (context.RequestAborted.IsCancellationRequested
                                   && !context.Response.HasStarted
                                   && CancelamentoDaRequisicao.Eh(ex))
        {
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            RegistrarRequisicaoAbandonada(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private void RegistrarRequisicaoAbandonada(HttpContext context)
    {
        // Registro só o template da rota e o id do trace, nunca o caminho nem a query: o caminho pode carregar dado pessoal (o CPF
        // ou CNPJ de /api/clientes/documento/{numero}) e a query pode carregar token, e esta é a única linha de log da requisição.
        var rota = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(rota desconhecida)";
        _logger.LogInformation(
            "Requisição cancelada pelo cliente: {Rota} (trace {TraceId}).",
            rota,
            Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Com a resposta já iniciada o status e os cabeçalhos foram enviados: no Kestrel, alterá-los lança
        // InvalidOperationException e esconderia a exceção original, e acrescentar o JSON de erro a um corpo parcial só o
        // corromperia. Se foi o cliente que fechou a conexão, não há nada a abortar e registro só a informação. Nos demais
        // casos registro o erro e aborto a conexão: sem isso o Kestrel conclui a resposta em andamento normalmente e o
        // cliente recebe um corpo truncado com status 200, que parece completo.
        if (context.Response.HasStarted)
        {
            if (context.RequestAborted.IsCancellationRequested && CancelamentoDaRequisicao.Eh(exception))
            {
                RegistrarRequisicaoAbandonada(context);
                return;
            }

            _logger.LogError(exception, "Erro depois de a resposta já ter sido iniciada; a resposta não pode mais ser alterada: {Message}", exception.Message);
            context.Abort();
            return;
        }

        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse();

        switch (exception)
        {
            case DomainException domainEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Mensagem = domainEx.Message;
                errorResponse.Tipo = TipoErro.Negocio;
                _logger.LogWarning("Erro de domínio: {Message}", domainEx.Message);
                break;

            case ConflitoException conflitoEx:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.Mensagem = conflitoEx.Message;
                errorResponse.Tipo = TipoErro.Conflito;
                _logger.LogWarning("Conflito de estado: {Message}", conflitoEx.Message);
                break;

            case ValidationException validationEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Mensagem = "Erro de validação.";
                errorResponse.Tipo = TipoErro.Validacao;
                errorResponse.Erros = validationEx.Errors
                    .Select(e => new ErrorDetail(e.PropertyName, e.ErrorMessage))
                    .ToList();
                _logger.LogWarning("Erro de validação: {Errors}", string.Join(", ", validationEx.Errors.Select(e => e.ErrorMessage)));
                break;

            case KeyNotFoundException:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.Mensagem = "Recurso não encontrado.";
                errorResponse.Tipo = TipoErro.NaoEncontrado;
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Mensagem = "Ocorreu um erro interno no servidor.";
                errorResponse.Tipo = TipoErro.Interno;
                _logger.LogError(exception, "Erro não tratado: {Message}", exception.Message);
                break;
        }

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
    }
}

public class ErrorResponse
{
    public string Tipo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public List<ErrorDetail>? Erros { get; set; }
}

public record ErrorDetail(string Campo, string Mensagem);
