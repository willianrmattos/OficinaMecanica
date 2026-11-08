using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Saga;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace OficinaMecanica.Infrastructure.Data;

public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    // Domain events de um SaveChanges feito dentro de uma transacao que ainda nao foi confirmada. Quem os
    // despacha (ou descarta) e o DomainEventsPosCommitInterceptor, quando a transacao termina.
    private readonly List<DomainEvent> _eventosAguardandoCommit = new();

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Peca> Pecas => Set<Peca>();
    public DbSet<OrdemDeServico> OrdensDeServico => Set<OrdemDeServico>();
    public DbSet<ItemServico> ItensServico => Set<ItemServico>();
    public DbSet<ItemPeca> ItensPeca => Set<ItemPeca>();
    public DbSet<HistoricoStatus> HistoricoStatus => Set<HistoricoStatus>();
    public DbSet<SagaPrazo> SagaPrazos => Set<SagaPrazo>();

    public AppDbContext(DbContextOptions<AppDbContext> options, IMediator mediator) : base(options)
    {
        _mediator = mediator;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Tabelas do MassTransit: o inbox deduplica por MessageId, o outbox guarda o que sera entregue ao broker
        // e o estado do outbox controla a entrega. Ficam no mesmo banco para entrar na mesma transacao do SaveChanges.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        // A tabela de estado da saga (OrdemDeServicoSagas) no mesmo banco: o repositorio de saga do MassTransit usa este DbContext.
        new OrdemDeServicoSagaStateMap().Configure(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // EF Core marks new HistoricoStatus entities (created via domain methods with Guid.NewGuid())
        // as Modified instead of Added when they are detected in a tracked navigation collection,
        // because non-default GUIDs are assumed to be existing DB rows. Since HistoricoStatus is
        // immutable, any Modified state here means a newly created entity that must be inserted.
        foreach (var entry in ChangeTracker.Entries<HistoricoStatus>()
            .Where(e => e.State == EntityState.Modified)
            .ToList())
        {
            entry.State = EntityState.Added;
        }

        foreach (var entry in ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.State == EntityState.Modified)
            .ToList())
        {
            entry.Entity.IncrementVersion();
        }

        var domainEntities = ChangeTracker.Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        domainEntities.ForEach(e => e.ClearDomainEvents());

        // Lido antes do save: o commit implicito do EF (sem transacao do chamador) fecha a transacao dentro do
        // proprio SaveChanges e nao deixa rastro em CurrentTransaction.
        var transacaoDoChamadorAberta = Database.CurrentTransaction != null;

        var result = await base.SaveChangesAsync(cancellationToken);

        if (transacaoDoChamadorAberta)
        {
            // Consumer/saga: o MassTransit abre a transacao e o consumer pode ser repetido. Despachar agora
            // mandaria o e-mail de uma tentativa que ainda pode ser desfeita; o interceptor despacha no commit.
            _eventosAguardandoCommit.AddRange(domainEvents);
            return result;
        }

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }

    internal IReadOnlyList<DomainEvent> RetirarEventosAguardandoCommit()
    {
        var eventos = _eventosAguardandoCommit.ToList();
        _eventosAguardandoCommit.Clear();
        return eventos;
    }

    internal void DescartarEventosAguardandoCommit() => _eventosAguardandoCommit.Clear();
}
