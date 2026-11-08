using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// Provider sem SQL (EF InMemory, usado pelos testes que nao sobem Docker): o repositorio nao tem lock nem transacao a pedir e
// so le os vencidos em ordem. O caminho relacional (UPDLOCK, READPAST, INDEX) e provado contra o SQL Server nos testes Infra.
public class PrazoRepositoryNaoRelacionalTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _provedor;

    public PrazoRepositoryNaoRelacionalTests()
    {
        var banco = "prazos_" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PrazoRepositoryNaoRelacionalTests>());
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(banco));
        _provedor = services.BuildServiceProvider(validateScopes: true);
    }

    private async Task<T> NoEscopoAsync<T>(Func<AppDbContext, PrazoRepository, Task<T>> acao)
    {
        await using var escopo = _provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        return await acao(db, new PrazoRepository(db));
    }

    [Fact]
    public async Task Adicionar_DevePersistirOPrazoNaHoraSemDependerDeOutroSave()
    {
        var prazo = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Pagamento, Agora.AddHours(48));

        await NoEscopoAsync(async (_, repositorio) => { await repositorio.AdicionarAsync(prazo); return 0; });

        var gravado = await NoEscopoAsync((db, _) => db.SagaPrazos.AsNoTracking().SingleAsync());
        gravado.PrazoId.Should().Be(prazo.PrazoId);
        gravado.CorrelationId.Should().Be(prazo.CorrelationId);
        gravado.Tipo.Should().Be(TipoPrazo.Pagamento);
        gravado.ExpiraEm.Should().Be(prazo.ExpiraEm);
        gravado.Processado.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessarVencidos_DeveReivindicarSoOsVencidosNaoProcessadosEmOrdemEAteOLote()
    {
        var antigo = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Aprovacao, Agora.AddMinutes(-30));
        var recente = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Aprovacao, Agora.AddMinutes(-1));
        var futuro = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Pagamento, Agora.AddMinutes(1));
        var jaProcessado = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Pagamento, Agora.AddMinutes(-60));
        jaProcessado.MarcarProcessado();
        await NoEscopoAsync(async (db, repositorio) =>
        {
            db.SagaPrazos.AddRange(antigo, recente, futuro, jaProcessado);
            await db.SaveChangesAsync();
            return 0;
        });
        var entregues = new List<Guid>();

        var reivindicados = await NoEscopoAsync((_, repositorio) => repositorio.ProcessarVencidosAsync(
            Agora, 1, (prazo, _) => { entregues.Add(prazo.PrazoId); return Task.CompletedTask; }));

        reivindicados.Should().Be(1);
        entregues.Should().Equal(antigo.PrazoId);
        var estado = await NoEscopoAsync((db, _) => db.SagaPrazos.AsNoTracking().ToDictionaryAsync(p => p.PrazoId, p => p.Processado));
        estado[antigo.PrazoId].Should().BeTrue();
        estado[recente.PrazoId].Should().BeFalse("ficou fora do lote");
        estado[futuro.PrazoId].Should().BeFalse("ainda nao venceu");
    }

    [Fact]
    public async Task ProcessarVencidos_PrazoQueVenceExatamenteAgora_DeveSerReivindicado()
    {
        var noLimite = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Aprovacao, Agora);
        await NoEscopoAsync(async (_, repositorio) => { await repositorio.AdicionarAsync(noLimite); return 0; });
        var entregues = new List<Guid>();

        await NoEscopoAsync((_, repositorio) => repositorio.ProcessarVencidosAsync(
            Agora, 10, (prazo, _) => { entregues.Add(prazo.PrazoId); return Task.CompletedTask; }));

        entregues.Should().Equal(noLimite.PrazoId);
    }

    [Fact]
    public async Task ProcessarVencidos_SemVencidos_NaoDeveChamarOCallback()
    {
        var chamado = false;

        var reivindicados = await NoEscopoAsync((_, repositorio) => repositorio.ProcessarVencidosAsync(
            Agora, 10, (_, _) => { chamado = true; return Task.CompletedTask; }));

        reivindicados.Should().Be(0);
        chamado.Should().BeFalse();
    }
}
