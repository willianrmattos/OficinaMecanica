using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// Aplica as migrations num SQL Server de verdade, partindo do estado anterior (Initial), como acontece com um
// banco que ja tem dados.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class MigrationTests
{
    private const string MigrationInicial = "20260503122341_Initial";

    private readonly InfraFixture _infra;

    public MigrationTests(InfraFixture infra) => _infra = infra;

    private static ServiceProvider CriarProvedor(string connectionString)
    {
        var configuracao = ConfiguracaoDeTeste.Criar(("ConnectionStrings:DefaultConnection", connectionString));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuracao);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task<List<T>> LerAsync<T>(string connectionString, string sql, Func<SqlDataReader, T> mapear)
    {
        await using var conexao = new SqlConnection(connectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await using var leitor = await comando.ExecuteReaderAsync();

        var itens = new List<T>();
        while (await leitor.ReadAsync())
            itens.Add(mapear(leitor));
        return itens;
    }

    private static async Task ExecutarAsync(string connectionString, string sql)
    {
        await using var conexao = new SqlConnection(connectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Migrate_PartindoDoEstadoInicial_DeveAcrescentarColunasETabelasSemPerderDados()
    {
        var connectionString = await _infra.CriarBancoAsync();
        await using var provedor = CriarProvedor(connectionString);
        await using var escopo = provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(MigrationInicial);
        var osLegada = Guid.NewGuid();
        await ExecutarAsync(connectionString,
            $"INSERT INTO OrdensDeServico (Id, Numero, ClienteId, VeiculoId, Status, ValorTotal, DataAbertura, Version) " +
            $"VALUES ('{osLegada}', 'OS-LEGADA-1', NEWID(), NEWID(), 1, 100.00, SYSUTCDATETIME(), 0)");

        await db.Database.MigrateAsync();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();

        var colunas = await LerAsync(connectionString,
            "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_NAME = 'OrdensDeServico' AND COLUMN_NAME IN " +
            "('ParticipaDaSaga','EstoqueReservado','PagamentoId','LinkPagamento','PagamentoExpiraEm','MotivoCancelamento')",
            l => (Nome: l.GetString(0), Tipo: l.GetString(1), Anulavel: l.GetString(2), Padrao: l.IsDBNull(3) ? null : l.GetString(3)));

        colunas.Should().HaveCount(6);
        colunas.Single(c => c.Nome == "ParticipaDaSaga").Should().Be(("ParticipaDaSaga", "bit", "NO", "(CONVERT([bit],(0)))"));
        colunas.Single(c => c.Nome == "EstoqueReservado").Should().Be(("EstoqueReservado", "bit", "NO", "(CONVERT([bit],(0)))"));
        colunas.Where(c => c.Nome is "PagamentoId" or "LinkPagamento" or "PagamentoExpiraEm" or "MotivoCancelamento")
            .Should().OnlyContain(c => c.Anulavel == "YES");

        var legada = await LerAsync(connectionString,
            $"SELECT ParticipaDaSaga, EstoqueReservado FROM OrdensDeServico WHERE Id = '{osLegada}'",
            l => (Participa: l.GetBoolean(0), Reservado: l.GetBoolean(1)));
        legada.Should().ContainSingle().Which.Should().Be((false, false), "a OS anterior a saga nao participa dela");

        var tabelas = await LerAsync(connectionString,
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('OutboxMessage','OutboxState','InboxState')",
            l => l.GetString(0));
        tabelas.Should().BeEquivalentTo("OutboxMessage", "OutboxState", "InboxState");
    }

    [Fact]
    public async Task Migrate_BancoNovo_NaoDeveTerAlteracoesPendentesNoModelo()
    {
        var connectionString = await _infra.CriarBancoAsync();
        await using var provedor = CriarProvedor(connectionString);
        await using var escopo = provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        var act = () => db.Database.MigrateAsync();

        await act.Should().NotThrowAsync("o EF 9 recusa migrar quando o modelo tem mudancas sem migration");
    }
}
