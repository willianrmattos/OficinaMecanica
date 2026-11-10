using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// O claim de prazos contra o SQL Server de verdade: UPDLOCK + READPAST + INDEX fazem cada prazo ser reivindicado por UMA instancia
// do sweeper, mesmo com varias replicas (HPA) varrendo ao mesmo tempo.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class SweeperComSqlTests
{
    private const string FilaDosPrazos = "os-f3b-prazos-observados";

    private readonly InfraFixture _infra;

    public SweeperComSqlTests(InfraFixture infra) => _infra = infra;

    private static async Task SemearPrazosVencidosAsync(AmbienteDeInfra ambiente, int quantidade, DateTimeOffset agora)
    {
        var prazos = Enumerable.Range(0, quantidade)
            .Select(i => new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), i % 2 == 0 ? TipoPrazo.Aprovacao : TipoPrazo.Pagamento, agora.AddMinutes(-i - 1)))
            .ToArray<object>();
        await ambiente.SemearAsync(prazos);
    }

    private Task<AmbienteDeInfra> CriarAmbienteAsync() =>
        AmbienteDeInfra.CriarAsync(
            _infra,
            x => x.AddConsumer<ConsumerDePrazoExpirado>(),
            (contexto, bus) => bus.ReceiveEndpoint(FilaDosPrazos, endpoint =>
            {
                endpoint.ConfigurarRetryEOutbox(contexto);
                endpoint.ConfigureConsumer<ConsumerDePrazoExpirado>(contexto);
            }),
            configuracaoExtra: new (string, string?)[]
            {
                ("Saga:SweeperHabilitado", "false"), ("Saga:SweeperLote", "10")
            },
            compor: (servicos, cfg) => servicos.AddServicosDaSaga(cfg));

    [Fact]
    public async Task DuasInstanciasDoSweeperEmParalelo_DevemTratarCadaPrazoExatamenteUmaVez()
    {
        await using var ambiente = await CriarAmbienteAsync();
        const int total = 160;
        await SemearPrazosVencidosAsync(ambiente, total, DateTimeOffset.UtcNow);

        // Quatro "replicas" varrendo em paralelo, cada uma com o seu escopo (DbContext, conexao e outbox proprios).
        async Task<int> VarrerAteEsvaziarAsync()
        {
            var tratados = 0;
            int lote;
            do
            {
                await using var escopo = ambiente.CriarEscopo();
                lote = await escopo.ServiceProvider.GetRequiredService<SweeperDePrazos>().ProcessarLoteAsync();
                tratados += lote;
            }
            while (lote > 0);
            return tratados;
        }

        var porInstancia = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(VarrerAteEsvaziarAsync)));

        porInstancia.Sum().Should().Be(total, "cada prazo e reivindicado por exatamente uma instancia");
        (await Esperar.AteAsync(() => ambiente.Consumo.PrazosRecebidos.Count >= total, TimeSpan.FromSeconds(60))).Should().BeTrue();
        await Task.Delay(TimeSpan.FromSeconds(3));
        ambiente.Consumo.PrazosRecebidos.Should().HaveCount(total).And.OnlyHaveUniqueItems("nenhum PrazoExpirado foi publicado duas vezes");
        (await ambiente.ConsultarAsync(db => db.SagaPrazos.CountAsync(p => !p.Processado))).Should().Be(0);
        ambiente.Logs.Excecoes().Should().BeEmpty("0 deadlock e 0 violacao de chave");
    }

    [Fact]
    public async Task ClaimConcorrente_QuemSegurouAsLinhasNaoBloqueiaOutraInstanciaEOsLotesNaoSeSobrepoem()
    {
        await using var ambiente = await CriarAmbienteAsync();
        await SemearPrazosVencidosAsync(ambiente, 100, DateTimeOffset.UtcNow);
        var agora = DateTimeOffset.UtcNow.AddMinutes(1);
        var primeiraTemOsLocks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberarAPrimeira = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var daPrimeira = new List<Guid>();
        var daSegunda = new List<Guid>();

        // A primeira instancia reivindica 50 linhas e SEGURA a transacao aberta (callback pendurado) enquanto a segunda reivindica.
        await using var escopoA = ambiente.CriarEscopo();
        var repositorioA = new PrazoRepository(escopoA.ServiceProvider.GetRequiredService<AppDbContext>());
        var primeira = repositorioA.ProcessarVencidosAsync(agora, 50, async (prazo, _) =>
        {
            daPrimeira.Add(prazo.PrazoId);
            primeiraTemOsLocks.TrySetResult();
            await liberarAPrimeira.Task;
        });
        await primeiraTemOsLocks.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await using var escopoB = ambiente.CriarEscopo();
        var repositorioB = new PrazoRepository(escopoB.ServiceProvider.GetRequiredService<AppDbContext>());
        var segunda = repositorioB.ProcessarVencidosAsync(agora, 50, (prazo, _) =>
        {
            daSegunda.Add(prazo.PrazoId);
            return Task.CompletedTask;
        });

        // Sem READPAST a segunda ficaria esperando o lock da primeira; sem a dica INDEX(...) a primeira travaria todas as 100
        // linhas lidas pelo plano e a segunda sairia com zero.
        var concluiu = await Task.WhenAny(segunda, Task.Delay(TimeSpan.FromSeconds(15))) == segunda;
        concluiu.Should().BeTrue("a segunda instancia pula as linhas travadas (READPAST) em vez de esperar");
        (await segunda).Should().Be(50, "sobram 50 linhas livres: o INDEX faz a primeira travar so as 50 que leu");

        liberarAPrimeira.SetResult();
        (await primeira).Should().Be(50);
        daPrimeira.Intersect(daSegunda).Should().BeEmpty("nenhum prazo e entregue a duas instancias");
        daPrimeira.Concat(daSegunda).Should().OnlyHaveUniqueItems().And.HaveCount(100);
    }

    [Fact]
    public async Task Claim_DeveIgnorarPrazoFuturoEJaProcessadoEMarcarOsProcessados()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var agora = DateTimeOffset.UtcNow;
        var vencido = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Aprovacao, agora.AddMinutes(-5));
        var futuro = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Pagamento, agora.AddHours(2));
        var jaProcessado = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Pagamento, agora.AddMinutes(-30));
        jaProcessado.MarcarProcessado();
        await ambiente.SemearAsync(vencido, futuro, jaProcessado);

        await using var escopo = ambiente.CriarEscopo();
        var tratados = new List<Guid>();
        var quantidade = await new PrazoRepository(escopo.ServiceProvider.GetRequiredService<AppDbContext>())
            .ProcessarVencidosAsync(agora, 50, (prazo, _) => { tratados.Add(prazo.PrazoId); return Task.CompletedTask; });

        quantidade.Should().Be(1);
        tratados.Should().Equal(vencido.PrazoId);
        var estado = await ambiente.ConsultarAsync(db => db.SagaPrazos.AsNoTracking().ToDictionaryAsync(p => p.PrazoId, p => p.Processado));
        estado[vencido.PrazoId].Should().BeTrue();
        estado[futuro.PrazoId].Should().BeFalse();
    }

    [Fact]
    public async Task Claim_PrazoQueVenceExatamenteNoInstanteDaVarredura_DeveSerReivindicado()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var agora = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var noLimite = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Aprovacao, agora);
        var umTiqueDepois = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Pagamento, agora.AddTicks(10));
        await ambiente.SemearAsync(noLimite, umTiqueDepois);

        await using var escopo = ambiente.CriarEscopo();
        var tratados = new List<Guid>();
        await new PrazoRepository(escopo.ServiceProvider.GetRequiredService<AppDbContext>())
            .ProcessarVencidosAsync(agora, 50, (prazo, _) => { tratados.Add(prazo.PrazoId); return Task.CompletedTask; });

        tratados.Should().Equal(noLimite.PrazoId);
    }

    [Fact]
    public async Task FalhaAoPublicar_DeveDesfazerAMarcaDeProcessadoNoBanco()
    {
        await using var ambiente = await CriarAmbienteAsync();
        await SemearPrazosVencidosAsync(ambiente, 3, DateTimeOffset.UtcNow);
        await using var escopo = ambiente.CriarEscopo();

        var falha = async () => await new PrazoRepository(escopo.ServiceProvider.GetRequiredService<AppDbContext>())
            .ProcessarVencidosAsync(DateTimeOffset.UtcNow, 50, (_, _) => throw new FalhaTecnicaSimuladaException());

        await falha.Should().ThrowAsync<FalhaTecnicaSimuladaException>();
        (await ambiente.ConsultarAsync(db => db.SagaPrazos.CountAsync(p => !p.Processado))).Should().Be(3, "publicar e marcar comitam juntos");
    }

    [Fact]
    public async Task IndiceDoSweeper_DeveExistirComAsColunasEAOrdemQueADicaUsa()
    {
        await using var ambiente = await CriarAmbienteAsync();

        var colunas = await ambiente.ConsultarAsync(async db =>
        {
            await using var comando = db.Database.GetDbConnection().CreateCommand();
            await db.Database.OpenConnectionAsync();
            comando.CommandText =
                "SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id " +
                "JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id " +
                "WHERE i.name = 'IX_SagaPrazos_Processado_ExpiraEm' AND ic.is_included_column = 0 ORDER BY ic.key_ordinal";
            var nomes = new List<string>();
            await using var leitor = await comando.ExecuteReaderAsync();
            while (await leitor.ReadAsync())
                nomes.Add(leitor.GetString(0));
            return nomes;
        });

        colunas.Should().Equal("Processado", "ExpiraEm");
    }
}
