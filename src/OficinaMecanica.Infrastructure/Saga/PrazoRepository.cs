using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Data.Configurations;

namespace OficinaMecanica.Infrastructure.Saga;

public sealed class PrazoRepository : IPrazoRepository
{
    // UPDLOCK trava a linha lida e READPAST faz a outra replica pular a que ja esta travada, entao cada prazo e
    // reivindicado por uma instancia so. O que importa e que as dicas travam TUDO o que o plano LE, nao so as N
    // linhas devolvidas: sem a dica INDEX(...) o SQL Server pode ler a tabela inteira para ordenar, e a primeira
    // instancia travaria todas as linhas e deixaria as demais com zero. Com o indice (Processado, ExpiraEm) a leitura
    // sai ordenada e para na N-esima linha. O ORDER BY completo mantem essa ordem do indice.
    internal const string SqlDoClaim = $$"""
        SELECT TOP ({1}) [CorrelationId], [PrazoId], [Tipo], [ExpiraEm], [Processado]
        FROM [SagaPrazos] WITH (UPDLOCK, READPAST, ROWLOCK, INDEX({{SagaPrazoConfiguration.IndiceDoSweeper}}))
        WHERE [Processado] = 0 AND [ExpiraEm] <= {0}
        ORDER BY [ExpiraEm], [PrazoId]
        """;

    private readonly AppDbContext _db;

    public PrazoRepository(AppDbContext db)
    {
        _db = db;
    }

    // Grava na hora, como os commands da Application: dentro do consumer isso ainda e a transacao da saga (comita ou volta
    // junto com a OS, o estoque e as mensagens do outbox), e nao depende de quem fecha a unidade de trabalho salvar de novo.
    public async Task AdicionarAsync(SagaPrazo prazo, CancellationToken cancellationToken = default)
    {
        _db.SagaPrazos.Add(prazo);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ProcessarVencidosAsync(
        DateTimeOffset agora,
        int lote,
        Func<SagaPrazo, CancellationToken, Task> aoExpirar,
        CancellationToken cancellationToken = default)
    {
        // Provider sem SQL (EF InMemory dos testes): nao ha transacao nem lock a pedir, so a leitura em ordem.
        if (!_db.Database.IsRelational())
            return await ProcessarSemLockAsync(agora, lote, aoExpirar, cancellationToken);

        // Fora de consumer o EF tem retry de falha transitoria ligado, e ele so aceita transacao aberta pelo
        // chamador dentro de uma estrategia de execucao: o lote inteiro e a unidade que ele repete.
        var estrategia = _db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync(async () =>
        {
            // Uma repeticao do lote nao pode reaproveitar entidades de uma tentativa que falhou no meio.
            _db.ChangeTracker.Clear();

            await using var transacao = await _db.Database.BeginTransactionAsync(cancellationToken);

            var vencidos = await _db.SagaPrazos
                .FromSqlRaw(SqlDoClaim, agora, lote)
                .AsTracking()
                .ToListAsync(cancellationToken);

            await MarcarEPublicarAsync(vencidos, aoExpirar, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);

            return vencidos.Count;
        });
    }

    private async Task<int> ProcessarSemLockAsync(
        DateTimeOffset agora, int lote, Func<SagaPrazo, CancellationToken, Task> aoExpirar, CancellationToken cancellationToken)
    {
        var vencidos = await _db.SagaPrazos
            .Where(p => !p.Processado && p.ExpiraEm <= agora)
            .OrderBy(p => p.ExpiraEm).ThenBy(p => p.PrazoId)
            .Take(lote)
            .ToListAsync(cancellationToken);

        await MarcarEPublicarAsync(vencidos, aoExpirar, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return vencidos.Count;
    }

    private static async Task MarcarEPublicarAsync(
        IEnumerable<SagaPrazo> vencidos, Func<SagaPrazo, CancellationToken, Task> aoExpirar, CancellationToken cancellationToken)
    {
        foreach (var prazo in vencidos)
        {
            prazo.MarcarProcessado();
            await aoExpirar(prazo, cancellationToken);
        }
    }
}
