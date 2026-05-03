using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace OficinaMecanica.Infrastructure.Data;

public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Peca> Pecas => Set<Peca>();
    public DbSet<OrdemDeServico> OrdensDeServico => Set<OrdemDeServico>();
    public DbSet<ItemServico> ItensServico => Set<ItemServico>();
    public DbSet<ItemPeca> ItensPeca => Set<ItemPeca>();
    public DbSet<HistoricoStatus> HistoricoStatus => Set<HistoricoStatus>();

    public AppDbContext(DbContextOptions<AppDbContext> options, IMediator mediator) : base(options)
    {
        _mediator = mediator;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
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

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}
