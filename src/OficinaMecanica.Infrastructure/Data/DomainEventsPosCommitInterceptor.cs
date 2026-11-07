using System.Data.Common;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OficinaMecanica.Infrastructure.Data;

// Despacha os domain events que o AppDbContext guardou durante uma transacao do chamador (a do MassTransit num
// consumer ou saga) somente depois do COMMIT, e os descarta se a transacao for desfeita.
//
// O despacho usa um escopo novo: no escopo original o EF ainda mantem a transacao em Database.CurrentTransaction
// durante este callback, e um handler que escrevesse no banco falharia com "Cannot issue SAVE TRANSACTION when
// there is no active transaction". Um escopo novo traz um AppDbContext e uma conexao proprios.
public sealed class DomainEventsPosCommitInterceptor : DbTransactionInterceptor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DomainEventsPosCommitInterceptor> _logger;

    public DomainEventsPosCommitInterceptor(IServiceScopeFactory scopeFactory, ILogger<DomainEventsPosCommitInterceptor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AppDbContext db)
            await DespacharAsync(db, cancellationToken);

        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    // So existe para quem confirma a transacao de forma sincrona; o MassTransit confirma por Async, mas sem
    // este override um Commit() sincrono deixaria os eventos guardados sem nunca despachar.
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (eventData.Context is AppDbContext db)
            DespacharAsync(db, CancellationToken.None).GetAwaiter().GetResult();

        base.TransactionCommitted(transaction, eventData);
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        (eventData.Context as AppDbContext)?.DescartarEventosAguardandoCommit();
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        (eventData.Context as AppDbContext)?.DescartarEventosAguardandoCommit();
        base.TransactionRolledBack(transaction, eventData);
    }

    public override Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        (eventData.Context as AppDbContext)?.DescartarEventosAguardandoCommit();
        return base.TransactionFailedAsync(transaction, eventData, cancellationToken);
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
    {
        (eventData.Context as AppDbContext)?.DescartarEventosAguardandoCommit();
        base.TransactionFailed(transaction, eventData);
    }

    private async Task DespacharAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var eventos = db.RetirarEventosAguardandoCommit();
        if (eventos.Count == 0)
            return;

        await using var escopo = _scopeFactory.CreateAsyncScope();
        var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();

        foreach (var evento in eventos)
        {
            try
            {
                await mediator.Publish(evento, cancellationToken);
            }
            catch (Exception ex)
            {
                // A transacao ja foi confirmada: lancar daqui falharia a unidade de trabalho inteira (e o
                // consumer repetiria um efeito que ja foi gravado) por causa de um efeito colateral como o e-mail.
                _logger.LogError(ex, "Falha ao despachar {Evento} depois do commit; a transacao ja estava confirmada.", evento.GetType().Name);
            }
        }
    }
}
