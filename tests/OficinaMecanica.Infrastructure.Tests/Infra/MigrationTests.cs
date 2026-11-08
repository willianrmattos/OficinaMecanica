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
    private const string MigrationDoPagamento = "20261107181304_AdicionaPagamentoEMensageria";

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
    public async Task Migrate_DaMigrationDoPagamento_DeveCriarSagaEPrazosComChavesIndicesEDefaults()
    {
        var connectionString = await _infra.CriarBancoAsync();
        await using var provedor = CriarProvedor(connectionString);
        await using var escopo = provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationDoPagamento);
        var osExistente = Guid.NewGuid();
        await ExecutarAsync(connectionString,
            $"INSERT INTO OrdensDeServico (Id, Numero, ClienteId, VeiculoId, Status, ValorTotal, DataAbertura, Version, ParticipaDaSaga, EstoqueReservado) " +
            $"VALUES ('{osExistente}', 'OS-ANTES-DA-SAGA', NEWID(), NEWID(), 1, 100.00, SYSUTCDATETIME(), 0, 0, 0)");
        (await LerAsync(connectionString, "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('OrdemDeServicoSagas','SagaPrazos')", l => l.GetString(0)))
            .Should().BeEmpty("as tabelas da saga ainda nao existem antes da migration");

        await db.Database.MigrateAsync();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        var colunas = await LerAsync(connectionString,
            "SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_NAME IN ('OrdemDeServicoSagas','SagaPrazos') ORDER BY TABLE_NAME, ORDINAL_POSITION",
            l => (Tabela: l.GetString(0), Coluna: l.GetString(1), Tipo: l.GetString(2), Anulavel: l.GetString(3), Padrao: l.IsDBNull(4) ? null : l.GetString(4), Tamanho: l.IsDBNull(5) ? (int?)null : l.GetInt32(5)));

        colunas.Where(c => c.Tabela == "OrdemDeServicoSagas").Select(c => (c.Coluna, c.Tipo, c.Anulavel, c.Tamanho)).Should().Equal(
            ("CorrelationId", "uniqueidentifier", "NO", (int?)null),
            ("CurrentState", "nvarchar", "NO", 64),
            ("Numero", "nvarchar", "NO", 30),
            ("OrcamentoId", "uniqueidentifier", "YES", null),
            ("PagamentoId", "uniqueidentifier", "YES", null),
            ("PrazoAprovacaoId", "uniqueidentifier", "YES", null),
            ("PrazoPagamentoId", "uniqueidentifier", "YES", null),
            ("CancelamentoPorStaff", "bit", "NO", null),
            ("MotivoCancelamento", "nvarchar", "YES", 400),
            ("CriadaEm", "datetimeoffset", "NO", null),
            ("AtualizadaEm", "datetimeoffset", "NO", null));
        colunas.Single(c => c.Tabela == "OrdemDeServicoSagas" && c.Coluna == "CancelamentoPorStaff").Padrao.Should().Be("(CONVERT([bit],(0)))");
        colunas.Where(c => c.Tabela == "SagaPrazos").Select(c => (c.Coluna, c.Tipo, c.Anulavel, c.Tamanho)).Should().Equal(
            ("PrazoId", "uniqueidentifier", "NO", (int?)null),
            ("CorrelationId", "uniqueidentifier", "NO", null),
            ("Tipo", "nvarchar", "NO", 20),
            ("ExpiraEm", "datetimeoffset", "NO", null),
            ("Processado", "bit", "NO", null));
        colunas.Single(c => c.Tabela == "SagaPrazos" && c.Coluna == "Processado").Padrao.Should().Be("(CONVERT([bit],(0)))");

        var indices = await LerAsync(connectionString,
            "SELECT t.name, i.name, i.is_primary_key, i.is_unique, i.type_desc, " +
            "STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal) " +
            "FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id " +
            "JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0 " +
            "JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id " +
            "WHERE t.name IN ('OrdemDeServicoSagas','SagaPrazos') GROUP BY t.name, i.name, i.is_primary_key, i.is_unique, i.type_desc",
            l => (Tabela: l.GetString(0), Nome: l.GetString(1), Pk: l.GetBoolean(2), Unico: l.GetBoolean(3), Tipo: l.GetString(4), Colunas: l.GetString(5)));
        indices.Should().BeEquivalentTo(new[]
        {
            ("OrdemDeServicoSagas", "PK_OrdemDeServicoSagas", true, true, "CLUSTERED", "CorrelationId"),
            ("OrdemDeServicoSagas", "IX_OrdemDeServicoSagas_CurrentState", false, false, "NONCLUSTERED", "CurrentState"),
            ("SagaPrazos", "PK_SagaPrazos", true, true, "CLUSTERED", "PrazoId"),
            ("SagaPrazos", "IX_SagaPrazos_Processado_ExpiraEm", false, false, "NONCLUSTERED", "Processado,ExpiraEm")
        }, "o indice do sweeper tem as colunas e a ordem que a dica INDEX(...) do SQL do claim usa");

        // Os defaults valem na pratica: inserir sem as colunas com default as preenche.
        await ExecutarAsync(connectionString,
            "INSERT INTO SagaPrazos (PrazoId, CorrelationId, Tipo, ExpiraEm) VALUES (NEWID(), NEWID(), 'Aprovacao', SYSDATETIMEOFFSET())");
        await ExecutarAsync(connectionString,
            "INSERT INTO OrdemDeServicoSagas (CorrelationId, CurrentState, Numero, CriadaEm, AtualizadaEm) VALUES (NEWID(), 'AguardandoDiagnostico', 'OS-1', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())");
        (await LerAsync(connectionString, "SELECT Processado FROM SagaPrazos", l => l.GetBoolean(0))).Should().Equal(false);
        (await LerAsync(connectionString, "SELECT CancelamentoPorStaff FROM OrdemDeServicoSagas", l => l.GetBoolean(0))).Should().Equal(false);
        (await LerAsync(connectionString, $"SELECT Numero FROM OrdensDeServico WHERE Id = '{osExistente}'", l => l.GetString(0)))
            .Should().ContainSingle().Which.Should().Be("OS-ANTES-DA-SAGA");

        // O Down desfaz so o que a migration criou.
        await migrator.MigrateAsync(MigrationDoPagamento);
        (await LerAsync(connectionString, "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('OrdemDeServicoSagas','SagaPrazos')", l => l.GetString(0)))
            .Should().BeEmpty();
        (await LerAsync(connectionString, $"SELECT Numero FROM OrdensDeServico WHERE Id = '{osExistente}'", l => l.GetString(0)))
            .Should().ContainSingle("a OS existente sobrevive ao Down").Which.Should().Be("OS-ANTES-DA-SAGA");
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
