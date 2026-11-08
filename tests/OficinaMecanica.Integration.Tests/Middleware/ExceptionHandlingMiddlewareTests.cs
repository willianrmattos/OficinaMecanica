using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OficinaMecanica.API.Middleware;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Integration.Tests.Support;
using static OficinaMecanica.Integration.Tests.Support.MiddlewareDeTeste;

namespace OficinaMecanica.Integration.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static string TipoDoErro(DefaultHttpContext contexto) =>
        JsonDocument.Parse(CorpoEscrito(contexto)).RootElement.GetProperty("tipo").GetString()!;

    // Mapeamento de cada exceção de aplicação para o status, o tipo do erro, o que vai (e o que não vai) no corpo e o nível do log.

    [Fact]
    public async Task SemExcecao_DevePassarAdianteSemEscreverCorpo()
    {
        var contexto = NovoContexto();
        var middleware = new ExceptionHandlingMiddleware(_ => Task.CompletedTask, new Mock<ILogger<ExceptionHandlingMiddleware>>().Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be(200);
        CorpoEscrito(contexto).Should().BeEmpty();
    }

    [Fact]
    public async Task DomainException_DeveResponder400ComAMensagemDeNegocio()
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new DomainException("A OS já está finalizada."), logger.Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        TipoDoErro(contexto).Should().Be("Erro de Negócio");
        Registros(logger).Should().ContainSingle().Which.Nivel.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public async Task ConflitoException_DeveResponder409()
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new ConflitoException("Estado já alterado."), logger.Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        TipoDoErro(contexto).Should().Be("Conflito");
        Registros(logger).Should().ContainSingle().Which.Nivel.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public async Task ValidationException_DeveResponder400ComUmErroPorCampo()
    {
        var falhas = new[] { new ValidationFailure("Placa", "A placa é obrigatória.") };
        var contexto = NovoContexto();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ValidationException(falhas), new Mock<ILogger<ExceptionHandlingMiddleware>>().Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        TipoDoErro(contexto).Should().Be("Erro de Validação");
    }

    [Fact]
    public async Task KeyNotFoundException_DeveResponder404SemRepassarOTextoDaExcecao()
    {
        var contexto = NovoContexto();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new KeyNotFoundException("OS 123 não encontrada."), new Mock<ILogger<ExceptionHandlingMiddleware>>().Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        TipoDoErro(contexto).Should().Be("Não Encontrado");
        CorpoEscrito(contexto).Should().NotContain("123");
    }

    [Fact]
    public async Task ExcecaoInesperada_DeveResponder500SemVazarDetalhesEComErroLogado()
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto();
        var excecao = new InvalidOperationException("Server=tcp:banco;Password=segredo123 falhou");
        var middleware = new ExceptionHandlingMiddleware(_ => throw excecao, logger.Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        var corpo = CorpoEscrito(contexto);
        corpo.Should().Contain("Ocorreu um erro interno no servidor.").And.NotContain("segredo123");
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error).Which.Excecao.Should().BeSameAs(excecao);
    }

    // Cliente que fecha a conexão (timeout do chamador, do APIM ou do usuário): o RequestAborted dispara e quem usava o token
    // reage com OperationCanceledException (HttpClient, EF com o token já cancelado) ou, no caso de um comando SQL em voo,
    // com SqlException. Os testes abaixo fixam o que é abandono e o que continua erro.
    private static Exception Cancelamento(string tipo, CancellationToken token) => tipo == nameof(TaskCanceledException)
        ? new TaskCanceledException("A tarefa foi cancelada.", null, token)
        : new OperationCanceledException("The operation was canceled.", token);

    // Uma requisição abortada: o token já está cancelado quando a exceção sobe.
    private static async Task<(DefaultHttpContext Contexto, Mock<ILogger<ExceptionHandlingMiddleware>> Logger)> ExecutarAbortadaAsync(
        Func<Exception> excecao, Action<DefaultHttpContext>? preparar = null)
    {
        using var abortada = new CancellationTokenSource();
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto(abortada.Token);
        preparar?.Invoke(contexto);
        var middleware = new ExceptionHandlingMiddleware(
            _ =>
            {
                abortada.Cancel();
                throw excecao();
            },
            logger.Object);

        await middleware.InvokeAsync(contexto);

        return (contexto, logger);
    }

    [Theory]
    [InlineData(nameof(OperationCanceledException))]
    [InlineData(nameof(TaskCanceledException))]
    public async Task ClienteQueFechouAConexao_NaoDeveEscreverCorpoNemLogarErro(string tipo)
    {
        var (contexto, logger) = await ExecutarAbortadaAsync(
            () => Cancelamento(tipo, CancellationToken.None),
            c =>
            {
                c.Request.Path = "/api/ordens-de-servico/42/execucao";
                DefinirRota(c, "/api/ordens-de-servico/{id:guid}/execucao");
                c.Request.QueryString = new QueryString("?access_token=segredo-da-query");
                c.Request.Headers.Authorization = "Bearer segredo-do-cabecalho";
            });

        CorpoEscrito(contexto).Should().BeEmpty();
        contexto.Response.ContentType.Should().BeNull();
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
        var registros = Registros(logger);
        registros.Should().NotContain(r => r.Nivel >= LogLevel.Warning);
        registros.Should().ContainSingle(r => r.Nivel == LogLevel.Information)
            .Which.Mensagem.Should().Contain("/api/ordens-de-servico/{id:guid}/execucao")
            .And.NotContain("segredo");
        registros.Should().OnlyContain(r => r.Excecao == null);
    }

    [Fact]
    public async Task ClienteQueFechouAConexaoNaConsultaPorDocumento_DeveLogarOTemplateDaRotaENuncaOCpfDoPath()
    {
        // GET /api/clientes/documento/{numero} carrega o CPF/CNPJ no caminho: o log não pode registrar esse dado pessoal.
        // O template da rota por atributo não tem a barra inicial e preserva a caixa do nome do controller.
        var (_, logger) = await ExecutarAbortadaAsync(
            () => new OperationCanceledException(),
            c =>
            {
                c.Request.Path = "/api/clientes/documento/12345678901";
                DefinirRota(c, "api/Clientes/documento/{numero}");
            });

        var mensagem = Registros(logger).Should().ContainSingle().Which.Mensagem;
        mensagem.Should().Contain("api/Clientes/documento/{numero}");
        mensagem.Should().NotContain("12345678901");
    }

    [Fact]
    public async Task ClienteQueFechouAConexao_SemEndpoint_DeveLogarTextoFixoSemOCaminho()
    {
        var (_, logger) = await ExecutarAbortadaAsync(
            () => new OperationCanceledException(),
            c => c.Request.Path = "/api/clientes/documento/12345678901");

        var mensagem = Registros(logger).Should().ContainSingle().Which.Mensagem;
        mensagem.Should().Contain("(rota desconhecida)");
        mensagem.Should().NotContain("12345678901").And.NotContain("/api/clientes");
    }

    [Fact]
    public async Task ClienteQueFechouAConexaoComComandoSqlEmVoo_DeveResponder499SemCorpoNemErro()
    {
        var (contexto, logger) = await ExecutarAbortadaAsync(SqlExceptionDeTeste.CancelamentoDeComando);

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
        CorpoEscrito(contexto).Should().BeEmpty();
        Registros(logger).Should().NotContain(r => r.Nivel >= LogLevel.Warning);
    }

    [Fact]
    public async Task ClienteQueFechouAConexaoNoSaveChanges_DeveResponder499SemCorpoNemErro()
    {
        // O EF embrulha a SqlException de cancelamento em DbUpdateException.
        var (contexto, logger) = await ExecutarAbortadaAsync(
            () => new DbUpdateException("falha ao salvar", SqlExceptionDeTeste.CancelamentoDeComando()));

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
        CorpoEscrito(contexto).Should().BeEmpty();
        Registros(logger).Should().NotContain(r => r.Nivel >= LogLevel.Warning);
    }

    [Fact]
    public async Task ClienteQueFechouAConexaoComOceEmbrulhadaPeloEf_DeveResponder499()
    {
        var (contexto, logger) = await ExecutarAbortadaAsync(
            () => new DbUpdateException("falha ao salvar", new OperationCanceledException()));

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
        Registros(logger).Should().NotContain(r => r.Nivel >= LogLevel.Warning);
    }

    [Fact]
    public async Task CancelamentoDeComandoSqlComClienteAindaConectado_DeveContinuarRespondendo500ComErroLogado()
    {
        // Sem o RequestAborted cancelado não foi o cliente que saiu (por exemplo, um token de timeout interno).
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto(CancellationToken.None);
        var excecao = SqlExceptionDeTeste.CancelamentoDeComando();
        var middleware = new ExceptionHandlingMiddleware(_ => throw excecao, logger.Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be(500);
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error).Which.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task TimeoutDeComandoSql_ComClienteQueFechouAConexao_DeveContinuarRespondendo500ComErroLogado()
    {
        var excecao = SqlExceptionDeTeste.TimeoutDeComando();

        var (contexto, logger) = await ExecutarAbortadaAsync(() => excecao);

        contexto.Response.StatusCode.Should().Be(500);
        TipoDoErro(contexto).Should().Be("Erro Interno");
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error).Which.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task DeadlockSql_ComClienteQueFechouAConexao_DeveContinuarRespondendo500ComErroLogado()
    {
        var excecao = new DbUpdateException("falha ao salvar", SqlExceptionDeTeste.Deadlock());

        var (contexto, logger) = await ExecutarAbortadaAsync(() => excecao);

        contexto.Response.StatusCode.Should().Be(500);
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error).Which.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task SessaoSqlDerrubada_ComClienteQueFechouAConexao_DeveContinuarRespondendo500ComErroLogado()
    {
        var excecao = SqlExceptionDeTeste.SessaoDerrubada();

        var (contexto, logger) = await ExecutarAbortadaAsync(() => excecao);

        contexto.Response.StatusCode.Should().Be(500);
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error).Which.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task ChaveDuplicadaSql_ComClienteQueFechouAConexao_DeveContinuarRespondendo500ComErroLogado()
    {
        // O OS não mapeia violação de índice único para 409 (só ConflitoException): continua sendo erro interno.
        var excecao = new DbUpdateException("falha ao salvar", SqlExceptionDeTeste.IndiceUnicoDuplicado());

        var (contexto, logger) = await ExecutarAbortadaAsync(() => excecao);

        contexto.Response.StatusCode.Should().Be(500);
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error).Which.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task ClienteQueFechouAConexao_DeveLogarOTraceIdDaAtividadeCorrente()
    {
        using var atividade = new Activity("teste").Start();
        using var abortada = new CancellationTokenSource();
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(
            _ =>
            {
                abortada.Cancel();
                throw new OperationCanceledException(abortada.Token);
            },
            logger.Object);

        await middleware.InvokeAsync(NovoContexto(abortada.Token));

        Registros(logger).Should().ContainSingle().Which.Mensagem.Should().Contain(atividade.TraceId.ToString());
    }

    [Fact]
    public async Task ClienteQueFechouAConexao_SemAtividade_DeveLogarOIdDaRequisicao()
    {
        Activity.Current = null;
        using var abortada = new CancellationTokenSource();
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto(abortada.Token);
        contexto.TraceIdentifier = "requisicao-123";
        var middleware = new ExceptionHandlingMiddleware(
            _ =>
            {
                abortada.Cancel();
                throw new OperationCanceledException(abortada.Token);
            },
            logger.Object);

        await middleware.InvokeAsync(contexto);

        Registros(logger).Should().ContainSingle().Which.Mensagem.Should().Contain("requisicao-123");
    }

    [Theory]
    [InlineData(nameof(OperationCanceledException))]
    [InlineData(nameof(TaskCanceledException))]
    public async Task CancelamentoComClienteAindaConectado_DeveContinuarRespondendo500ComErroLogado(string tipo)
    {
        // Timeout interno de uma dependência (por exemplo, a chamada HTTP à Execução): o cliente segue esperando e
        // precisa receber a resposta de erro.
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto(CancellationToken.None);
        var excecao = Cancelamento(tipo, CancellationToken.None);
        var middleware = new ExceptionHandlingMiddleware(_ => throw excecao, logger.Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        TipoDoErro(contexto).Should().Be("Erro Interno");
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error)
            .Which.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task ExcecaoQueNaoEhCancelamento_ComClienteQueFechouAConexao_DeveContinuarRespondendo500ComErroLogado()
    {
        using var abortada = new CancellationTokenSource();
        abortada.Cancel();
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto(abortada.Token);
        var excecao = new InvalidOperationException("falha real");
        var middleware = new ExceptionHandlingMiddleware(_ => throw excecao, logger.Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        TipoDoErro(contexto).Should().Be("Erro Interno");
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error)
            .Which.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task DomainException_ComClienteQueFechouAConexao_DeveContinuarRespondendo400()
    {
        using var abortada = new CancellationTokenSource();
        abortada.Cancel();
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto(abortada.Token);
        var middleware = new ExceptionHandlingMiddleware(_ => throw new DomainException("A OS já está finalizada."), logger.Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        TipoDoErro(contexto).Should().Be("Erro de Negócio");
        Registros(logger).Should().ContainSingle().Which.Nivel.Should().Be(LogLevel.Warning);
    }

    // Com a resposta já iniciada o fake lança InvalidOperationException ao mudar status ou cabeçalhos, como o Kestrel: o
    // middleware não pode tocar na resposta. Quando o erro não é do cliente ter fechado a conexão, ele aborta a conexão
    // (Abort), porque sem isso o Kestrel conclui a resposta em andamento e o cliente recebe um corpo truncado que parece
    // completo.
    private static async Task<(DefaultHttpContext Contexto, CicloDeVidaDaRequisicao Ciclo, Mock<ILogger<ExceptionHandlingMiddleware>> Logger)>
        ExecutarComRespostaIniciadaAsync(Func<Exception> excecao, bool clienteFechouAConexao)
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto();
        var ciclo = new CicloDeVidaDaRequisicao();
        contexto.Features.Set<IHttpRequestLifetimeFeature>(ciclo);
        contexto.Features.Set<IHttpResponseFeature>(new RespostaJaIniciada());
        var middleware = new ExceptionHandlingMiddleware(
            _ =>
            {
                if (clienteFechouAConexao)
                    ciclo.ClienteFechouAConexao();
                throw excecao();
            },
            logger.Object);

        await middleware.InvokeAsync(contexto);

        return (contexto, ciclo, logger);
    }

    private static void DeveTerDeixadoARespostaIntacta(DefaultHttpContext contexto)
    {
        contexto.Response.StatusCode.Should().Be(200, "o status já enviado não pode mudar");
        contexto.Response.Headers.Should().BeEmpty();
        CorpoEscrito(contexto).Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(OperationCanceledException))]
    [InlineData(nameof(SqlExceptionDeTeste))]
    public async Task ClienteQueFechouAConexaoComRespostaJaIniciada_DeveLogarInformationSemAbortarNemLancar(string origem)
    {
        var (contexto, ciclo, logger) = await ExecutarComRespostaIniciadaAsync(
            () => origem == nameof(SqlExceptionDeTeste) ? SqlExceptionDeTeste.CancelamentoDeComando() : new OperationCanceledException(),
            clienteFechouAConexao: true);

        DeveTerDeixadoARespostaIntacta(contexto);
        ciclo.AbortFoiChamado.Should().BeFalse("a conexão já fechou; não há nada a abortar");
        contexto.RequestAborted.IsCancellationRequested.Should().BeTrue();
        var registros = Registros(logger);
        registros.Should().ContainSingle().Which.Nivel.Should().Be(LogLevel.Information);
        registros.Should().NotContain(r => r.Nivel >= LogLevel.Warning);
    }

    [Fact]
    public async Task CancelamentoInternoComRespostaJaIniciada_DeveLogarErroEAbortarAConexao()
    {
        // O token da requisição não foi cancelado: não foi o cliente que saiu (por exemplo, o timeout de uma dependência).
        var excecao = new OperationCanceledException();

        var (contexto, ciclo, logger) = await ExecutarComRespostaIniciadaAsync(() => excecao, clienteFechouAConexao: false);

        DeveTerDeixadoARespostaIntacta(contexto);
        ciclo.AbortFoiChamado.Should().BeTrue();
        contexto.RequestAborted.IsCancellationRequested.Should().BeTrue();
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error).Which.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task ExcecaoDeNegocioComRespostaJaIniciada_DeveLogarErroEAbortarAConexaoSemMexerNaResposta()
    {
        var excecao = new DomainException("regra violada no meio da resposta");

        var (contexto, ciclo, logger) = await ExecutarComRespostaIniciadaAsync(() => excecao, clienteFechouAConexao: false);

        DeveTerDeixadoARespostaIntacta(contexto);
        ciclo.AbortFoiChamado.Should().BeTrue();
        contexto.RequestAborted.IsCancellationRequested.Should().BeTrue();
        var registro = Registros(logger).Should().ContainSingle("o erro de domínio não vira Warning quando não há resposta a dar").Subject;
        registro.Nivel.Should().Be(LogLevel.Error);
        registro.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task ExcecaoInesperadaComRespostaJaIniciada_DeveLogarErroEAbortarAConexaoSemMexerNaResposta()
    {
        var excecao = new InvalidOperationException("falha no meio do corpo");

        var (contexto, ciclo, logger) = await ExecutarComRespostaIniciadaAsync(() => excecao, clienteFechouAConexao: false);

        DeveTerDeixadoARespostaIntacta(contexto);
        ciclo.AbortFoiChamado.Should().BeTrue();
        contexto.RequestAborted.IsCancellationRequested.Should().BeTrue();
        Registros(logger).Should().ContainSingle(r => r.Nivel == LogLevel.Error).Which.Excecao.Should().BeSameAs(excecao);
    }

    [Fact]
    public async Task ErroComRespostaAindaNaoIniciada_DeveResponder500SemAbortarAConexao()
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = NovoContexto();
        var ciclo = new CicloDeVidaDaRequisicao();
        contexto.Features.Set<IHttpRequestLifetimeFeature>(ciclo);
        var middleware = new ExceptionHandlingMiddleware(_ => throw new InvalidOperationException("falha"), logger.Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        ciclo.AbortFoiChamado.Should().BeFalse();
    }

    [Fact]
    public void FakeDeRespostaIniciada_DeveRecusarMudancaDeStatusECabecalhosComoOKestrel()
    {
        var contexto = NovoContexto();
        contexto.Features.Set<IHttpResponseFeature>(new RespostaJaIniciada());

        var mudarStatus = () => contexto.Response.StatusCode = 500;
        var mudarTipo = () => contexto.Response.ContentType = "application/json";

        mudarStatus.Should().Throw<InvalidOperationException>();
        mudarTipo.Should().Throw<InvalidOperationException>();
    }
}
