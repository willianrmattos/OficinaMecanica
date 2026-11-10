using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Infrastructure.Tests.Data;

// O AppDbContext decide, a cada SaveChanges, entre despachar os domain events na hora (caminho HTTP) e guarda-los
// ate o commit (transacao do chamador). Estes testes rodam sem Docker: o EF InMemory nao tem transacao, entao a
// transacao do chamador e simulada por um gerenciador de transacao que sempre devolve uma CurrentTransaction.
public class DespachoDeDomainEventsDoAppDbContextTests
{
    private sealed class RegistroDeDespachos
    {
        public List<OrdemDeServicoCriadaEvent> Despachados { get; } = new();
    }

    private sealed class RegistradorDeOrdemCriada : INotificationHandler<OrdemDeServicoCriadaEvent>
    {
        private readonly RegistroDeDespachos _registro;

        public RegistradorDeOrdemCriada(RegistroDeDespachos registro) => _registro = registro;

        public Task Handle(OrdemDeServicoCriadaEvent notification, CancellationToken cancellationToken)
        {
            _registro.Despachados.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class TransacaoSimulada : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public void Commit() { }
        public void Rollback() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class GerenteDeTransacaoComTransacaoAberta : IDbContextTransactionManager
    {
        public IDbContextTransaction? CurrentTransaction { get; } = new TransacaoSimulada();
        public IDbContextTransaction BeginTransaction() => CurrentTransaction!;
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentTransaction!);
        public void CommitTransaction() { }
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RollbackTransaction() { }
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ResetState() { }
        public Task ResetStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static ServiceProvider CriarProvedor(bool comTransacaoDoChamador)
    {
        var banco = "DespachoDeEventos_" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<RegistroDeDespachos>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DespachoDeDomainEventsDoAppDbContextTests>());
        services.AddDbContext<AppDbContext>(opcoes =>
        {
            opcoes.UseInMemoryDatabase(banco);
            if (comTransacaoDoChamador)
                opcoes.ReplaceService<IDbContextTransactionManager, GerenteDeTransacaoComTransacaoAberta>();
        });
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task SaveChanges_SemTransacaoDoChamador_DeveDespacharOsDomainEventsLogoDepoisDoSave()
    {
        await using var provedor = CriarProvedor(comTransacaoDoChamador: false);
        await using var escopo = provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        var ordem = new OrdemDeServicoBuilder().Recebida();
        db.Add(ordem);

        await db.SaveChangesAsync();

        var despachos = provedor.GetRequiredService<RegistroDeDespachos>().Despachados;
        despachos.Should().ContainSingle().Which.OrdemDeServicoId.Should().Be(ordem.Id);
        db.RetirarEventosAguardandoCommit().Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChanges_ComTransacaoDoChamador_DeveGuardarOsDomainEventsEmVezDeDespachar()
    {
        await using var provedor = CriarProvedor(comTransacaoDoChamador: true);
        await using var escopo = provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        var ordem = new OrdemDeServicoBuilder().Recebida();
        db.Add(ordem);

        db.Database.CurrentTransaction.Should().NotBeNull("a transacao do chamador esta simulada");
        await db.SaveChangesAsync();

        provedor.GetRequiredService<RegistroDeDespachos>().Despachados.Should().BeEmpty("a transacao ainda pode ser desfeita");
        var guardados = db.RetirarEventosAguardandoCommit();
        guardados.Should().ContainSingle().Which.Should().BeOfType<OrdemDeServicoCriadaEvent>()
            .Which.OrdemDeServicoId.Should().Be(ordem.Id);
    }

    [Fact]
    public async Task RetirarEventosAguardandoCommit_DeveEsvaziarOBuffer()
    {
        await using var provedor = CriarProvedor(comTransacaoDoChamador: true);
        await using var escopo = provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(new OrdemDeServicoBuilder().Recebida());
        await db.SaveChangesAsync();

        db.RetirarEventosAguardandoCommit().Should().HaveCount(1);

        db.RetirarEventosAguardandoCommit().Should().BeEmpty("quem retira os eventos assume o despacho deles");
    }

    [Fact]
    public async Task DescartarEventosAguardandoCommit_DeveLimparOBufferSemDespachar()
    {
        await using var provedor = CriarProvedor(comTransacaoDoChamador: true);
        await using var escopo = provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(new OrdemDeServicoBuilder().Recebida());
        await db.SaveChangesAsync();

        db.DescartarEventosAguardandoCommit();

        db.RetirarEventosAguardandoCommit().Should().BeEmpty();
        provedor.GetRequiredService<RegistroDeDespachos>().Despachados.Should().BeEmpty();
    }
}
