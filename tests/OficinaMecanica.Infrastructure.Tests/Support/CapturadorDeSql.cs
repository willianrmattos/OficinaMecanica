using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// Guarda o texto de cada comando que o AppDbContext manda ao SQL Server e se ele rodou dentro de uma transacao. O teste de
// bloqueio da saga o usa para afirmar o SQL de verdade: a leitura da linha da saga dentro da transacao do consumer.
public sealed class CapturadorDeSql : DbCommandInterceptor
{
    private readonly ConcurrentQueue<ComandoCapturado> _comandos = new();

    public IReadOnlyList<ComandoCapturado> Comandos => _comandos.ToArray();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Registrar(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Registrar(command);
        return ValueTask.FromResult(result);
    }

    private void Registrar(DbCommand comando) => _comandos.Enqueue(new ComandoCapturado(comando.CommandText, comando.Transaction is not null));
}

public sealed record ComandoCapturado(string Sql, bool EmTransacao);
