using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OficinaMecanica.Infrastructure.Data;

// O SQL Server e a dependencia sem a qual a API nao aceita escrita (o outbox mora nele): com o banco fora, o pod sai
// de rotacao. O tempo e limitado porque o retry do EF (EnableRetryOnFailure) esperaria varios segundos antes de
// desistir de um banco parado e a probe nao pode ficar pendurada.
public sealed class SqlServerHealthCheck : IHealthCheck
{
    private static readonly TimeSpan LimiteDaConexao = TimeSpan.FromSeconds(3);

    private readonly AppDbContext _db;

    public SqlServerHealthCheck(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(LimiteDaConexao);

        try
        {
            return await _db.Database.CanConnectAsync(limite.Token)
                ? HealthCheckResult.Healthy("Conexão com o banco de dados estabelecida.")
                : HealthCheckResult.Unhealthy("Não foi possível conectar ao banco de dados.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Não foi possível conectar ao banco de dados.", ex);
        }
    }
}
