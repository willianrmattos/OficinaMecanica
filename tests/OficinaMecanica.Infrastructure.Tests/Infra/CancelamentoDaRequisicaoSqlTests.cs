using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OficinaMecanica.API.Middleware;
using OficinaMecanica.Application;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// O critério de CancelamentoDaRequisicao é validado contra as exceções que o SqlClient e o EF realmente lançam num SQL Server
// real, não contra exceções montadas à mão: o cliente "sai" com um comando em voo e o middleware precisa responder 499 sem
// corpo e sem Error; os erros reais de SQL que acontecem com a requisição abortada precisam continuar como erro.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public sealed class CancelamentoDaRequisicaoSqlTests
{
    private readonly InfraFixture _infra;

    public CancelamentoDaRequisicaoSqlTests(InfraFixture infra) => _infra = infra;

    private sealed record Ambiente(ServiceProvider Provedor, string ConnectionString) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provedor.DisposeAsync();
    }

    // O mesmo registro do caminho HTTP (AddInfrastructure, com o retry do EF): o retry não pode mudar o tipo da exceção de
    // cancelamento.
    private async Task<Ambiente> CriarAmbienteMigradoAsync()
    {
        var conexao = await _infra.CriarBancoAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(ConfiguracaoDeTeste.Criar(("ConnectionStrings:DefaultConnection", conexao)));
        var provedor = services.BuildServiceProvider(validateScopes: true);

        await using var escopo = provedor.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        return new Ambiente(provedor, conexao);
    }

    // Contexto avulso, sem retry e sem o DI: serve para segurar um lock em outra transação e para o timeout de comando curto.
    private static AppDbContext NovoDbAvulso(string conexao, int? timeoutDeComando = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(conexao, sql =>
                {
                    if (timeoutDeComando is not null)
                        sql.CommandTimeout(timeoutDeComando);
                })
                .Options,
            Mock.Of<IMediator>());

    private sealed record Resultado(DefaultHttpContext Contexto, Mock<ILogger<ExceptionHandlingMiddleware>> Logger, Exception? Capturada, string Corpo);

    // clienteSaiNoMeio: o RequestAborted é cancelado enquanto o comando está em voo (o token vai para o EF/SqlClient, como no
    // caminho HTTP). Falso: o token já está cancelado quando o erro real de SQL acontece, e a operação roda sem token.
    private static async Task<Resultado> ExecutarNoMiddlewareAsync(Func<CancellationToken, Task> operacao, bool clienteSaiNoMeio)
    {
        using var abortada = new CancellationTokenSource();
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var contexto = new DefaultHttpContext { RequestAborted = abortada.Token };
        contexto.Response.Body = new MemoryStream();
        Exception? capturada = null;

        if (clienteSaiNoMeio)
            abortada.CancelAfter(TimeSpan.FromMilliseconds(1500));
        else
            abortada.Cancel();

        var middleware = new ExceptionHandlingMiddleware(
            async _ =>
            {
                try
                {
                    await operacao(clienteSaiNoMeio ? contexto.RequestAborted : CancellationToken.None);
                }
                catch (Exception ex)
                {
                    capturada = ex;
                    throw;
                }
            },
            logger.Object);

        await middleware.InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        return new Resultado(contexto, logger, capturada, new StreamReader(contexto.Response.Body).ReadToEnd());
    }

    private static List<LogLevel> NiveisLogados(Resultado r) =>
        r.Logger.Invocations
            .Where(i => i.Method.Name == nameof(ILogger.Log))
            .Select(i => (LogLevel)i.Arguments[0])
            .ToList();

    private static void DeveTerSidoAbandono(Resultado r)
    {
        r.Contexto.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
        r.Corpo.Should().BeEmpty();
        NiveisLogados(r).Should().NotContain(nivel => nivel >= LogLevel.Warning);
        CancelamentoDaRequisicao.Eh(r.Capturada!).Should().BeTrue();
    }

    private static void DeveTerSidoErroInterno(Resultado r)
    {
        r.Contexto.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        NiveisLogados(r).Should().ContainSingle().Which.Should().Be(LogLevel.Error);
        CancelamentoDaRequisicao.Eh(r.Capturada!).Should().BeFalse();
    }

    // ---- O cliente sai com o comando em voo ----

    [Fact]
    public async Task ComandoSqlEmVooCancelado_DeveSerSqlExceptionEOMiddlewareDeveResponder499SemCorpoNemErro()
    {
        await using var ambiente = await CriarAmbienteMigradoAsync();
        await using var escopo = ambiente.Provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        var r = await ExecutarNoMiddlewareAsync(
            token => db.Database.ExecuteSqlRawAsync("WAITFOR DELAY '00:00:30'", token), clienteSaiNoMeio: true);

        r.Capturada.Should().BeOfType<SqlException>();
        DeveTerSidoAbandono(r);
    }

    [Fact]
    public async Task ConsultaSqlEmVooCancelada_DeveSerSqlExceptionEOMiddlewareDeveResponder499SemCorpoNemErro()
    {
        await using var ambiente = await CriarAmbienteMigradoAsync();
        await using var escopo = ambiente.Provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        var r = await ExecutarNoMiddlewareAsync(
            async token => await db.Database.SqlQueryRaw<int>("WAITFOR DELAY '00:00:30'; SELECT 1 AS Value").ToListAsync(token),
            clienteSaiNoMeio: true);

        r.Capturada.Should().BeOfType<SqlException>();
        DeveTerSidoAbandono(r);
    }

    [Fact]
    public async Task SaveChangesBloqueadoEmVooCancelado_DeveSerDbUpdateExceptionComSqlExceptionEOMiddlewareDeveResponder499()
    {
        // Outra transação segura, sem confirmar, o mesmo documento (índice único): o SaveChanges espera o lock até o cliente sair.
        await using var ambiente = await CriarAmbienteMigradoAsync();
        await using var seguradora = NovoDbAvulso(ambiente.ConnectionString);
        await using var transacao = await seguradora.Database.BeginTransactionAsync();
        seguradora.Clientes.Add(new ClienteBuilder().ComDocumento("52998224725").Build());
        await seguradora.SaveChangesAsync();

        await using var escopo = ambiente.Provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Clientes.Add(new ClienteBuilder().ComDocumento("52998224725").Build());

        var r = await ExecutarNoMiddlewareAsync(token => db.SaveChangesAsync(token), clienteSaiNoMeio: true);

        r.Capturada.Should().BeOfType<DbUpdateException>().Which.InnerException.Should().BeOfType<SqlException>();
        DeveTerSidoAbandono(r);
        await transacao.RollbackAsync();
    }

    [Fact]
    public async Task TokenJaCanceladoAntesDoComando_DeveSerOceEOMiddlewareDeveResponder499()
    {
        await using var ambiente = await CriarAmbienteMigradoAsync();
        await using var escopo = ambiente.Provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        using var cancelado = new CancellationTokenSource();
        cancelado.Cancel();

        var r = await ExecutarNoMiddlewareAsync(
            _ => db.Database.ExecuteSqlRawAsync("SELECT 1", cancelado.Token), clienteSaiNoMeio: false);

        r.Capturada.Should().BeAssignableTo<OperationCanceledException>();
        DeveTerSidoAbandono(r);
    }

    // ---- Erros reais de SQL com a requisição já abortada: continuam sendo erro ----

    [Fact]
    public async Task TimeoutDeComandoReal_ComRequisicaoAbortada_DeveContinuarRespondendo500ComErroLogado()
    {
        await using var ambiente = await CriarAmbienteMigradoAsync();
        await using var db = NovoDbAvulso(ambiente.ConnectionString, timeoutDeComando: 1);

        var r = await ExecutarNoMiddlewareAsync(
            token => db.Database.ExecuteSqlRawAsync("WAITFOR DELAY '00:00:10'", token), clienteSaiNoMeio: false);

        r.Capturada.Should().BeOfType<SqlException>().Which.Number.Should().Be(-2);
        DeveTerSidoErroInterno(r);
        r.Corpo.Should().Contain("Erro Interno");
    }

    [Fact]
    public async Task IndiceUnicoDuplicadoReal_ComRequisicaoAbortada_DeveContinuarRespondendo500ComErroLogado()
    {
        await using var ambiente = await CriarAmbienteMigradoAsync();
        await using (var primeiro = ambiente.Provedor.CreateAsyncScope())
        {
            var db1 = primeiro.ServiceProvider.GetRequiredService<AppDbContext>();
            db1.Clientes.Add(new ClienteBuilder().ComDocumento("52998224725").Build());
            await db1.SaveChangesAsync();
        }

        await using var escopo = ambiente.Provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Clientes.Add(new ClienteBuilder().ComDocumento("52998224725").Build());

        var r = await ExecutarNoMiddlewareAsync(token => db.SaveChangesAsync(token), clienteSaiNoMeio: false);

        r.Capturada.Should().BeOfType<DbUpdateException>().Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Number.Should().Be(2601);
        DeveTerSidoErroInterno(r);
    }

    [Fact]
    public async Task DeadlockReal_ComRequisicaoAbortada_DeveContinuarRespondendo500ComErroLogado()
    {
        await using var ambiente = await CriarAmbienteMigradoAsync();
        await using (var preparo = new SqlConnection(ambiente.ConnectionString))
        {
            await preparo.OpenAsync();
            await using var criar = preparo.CreateCommand();
            criar.CommandText = "CREATE TABLE dbo.Deadlock (Id INT PRIMARY KEY, V INT); INSERT INTO dbo.Deadlock VALUES (1, 0), (2, 0)";
            await criar.ExecuteNonQueryAsync();
        }

        await using var outra = new SqlConnection(ambiente.ConnectionString);
        await outra.OpenAsync();
        await using var transacaoDaOutra = outra.BeginTransaction();
        await ExecutarAsync(outra, transacaoDaOutra, "SET DEADLOCK_PRIORITY HIGH; UPDATE dbo.Deadlock SET V = 1 WHERE Id = 1");

        Task? esperaDaOutra = null;
        var r = await ExecutarNoMiddlewareAsync(async _ =>
        {
            // Esta sessão é a vítima escolhida (prioridade baixa): segura a linha 2, espera a outra pedir a mesma linha e
            // então pede a linha 1.
            await using var vitima = new SqlConnection(ambiente.ConnectionString);
            await vitima.OpenAsync();
            await using var transacaoDaVitima = vitima.BeginTransaction();
            await ExecutarAsync(vitima, transacaoDaVitima, "SET DEADLOCK_PRIORITY LOW; UPDATE dbo.Deadlock SET V = 1 WHERE Id = 2");
            esperaDaOutra = ExecutarAsync(outra, transacaoDaOutra, "UPDATE dbo.Deadlock SET V = 2 WHERE Id = 2");
            await Task.Delay(1000);
            await ExecutarAsync(vitima, transacaoDaVitima, "UPDATE dbo.Deadlock SET V = 2 WHERE Id = 1");
        }, clienteSaiNoMeio: false);

        await esperaDaOutra!;
        await transacaoDaOutra.RollbackAsync();
        r.Capturada.Should().BeOfType<SqlException>().Which.Number.Should().Be(1205);
        DeveTerSidoErroInterno(r);
    }

    [Fact]
    public async Task SessaoDerrubadaReal_ComRequisicaoAbortada_DeveContinuarRespondendo500ComErroLogado()
    {
        await using var ambiente = await CriarAmbienteMigradoAsync();
        var administracao = new SqlConnectionStringBuilder(ambiente.ConnectionString) { InitialCatalog = "master" }.ConnectionString;

        var r = await ExecutarNoMiddlewareAsync(async semToken =>
        {
            await using var conexao = new SqlConnection(ambiente.ConnectionString);
            await conexao.OpenAsync();
            short sessao;
            await using (var consulta = conexao.CreateCommand())
            {
                consulta.CommandText = "SELECT @@SPID";
                sessao = Convert.ToInt16(await consulta.ExecuteScalarAsync());
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                await using var administrador = new SqlConnection(administracao);
                await administrador.OpenAsync();
                await using var kill = administrador.CreateCommand();
                kill.CommandText = $"KILL {sessao}";
                await kill.ExecuteNonQueryAsync();
            });

            await using var longo = conexao.CreateCommand();
            longo.CommandText = "WAITFOR DELAY '00:00:30'";
            await longo.ExecuteNonQueryAsync();
        }, clienteSaiNoMeio: false);

        r.Capturada.Should().BeOfType<SqlException>().Which.Number.Should().Be(596);
        DeveTerSidoErroInterno(r);
    }

    private static async Task ExecutarAsync(SqlConnection conexao, SqlTransaction transacao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }
}
