using FluentAssertions;
using OficinaMecanica.Infrastructure.Data.Configurations;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// O SQL do claim e a parte da saga que depende de dicas de lock: cada uma tem um motivo e nenhuma pode sumir sem os testes
// reprovarem. O comportamento com duas instancias esta provado contra o SQL Server nos testes Infra (SweeperComSqlTests); aqui
// fica o contrato do texto, que roda sem Docker.
public class PrazoRepositorySqlDoClaimTests
{
    [Fact]
    public void Sql_DeveTravarAsLinhasComUpdlockPularAsTravadasComReadpastELerPeloIndiceDoSweeper()
    {
        var sql = PrazoRepository.SqlDoClaim;

        sql.Should().Contain("WITH (UPDLOCK, READPAST, ROWLOCK, INDEX(IX_SagaPrazos_Processado_ExpiraEm))");
        SagaPrazoConfiguration.IndiceDoSweeper.Should().Be("IX_SagaPrazos_Processado_ExpiraEm", "o SQL cita o indice pelo nome");
    }

    [Fact]
    public void Sql_DeveReivindicarSoOsVencidosNaoProcessadosEmOrdemDoIndiceELimitarPeloLote()
    {
        var sql = PrazoRepository.SqlDoClaim;

        sql.Should().Contain("TOP ({1})");
        sql.Should().Contain("[Processado] = 0 AND [ExpiraEm] <= {0}");
        sql.Should().Contain("ORDER BY [ExpiraEm], [PrazoId]");
        sql.Should().Contain("FROM [SagaPrazos]");
    }

    [Fact]
    public void Sql_DeveListarTodasAsColunasDaEntidade()
    {
        var sql = PrazoRepository.SqlDoClaim;

        foreach (var coluna in new[] { "CorrelationId", "PrazoId", "Tipo", "ExpiraEm", "Processado" })
            sql.Should().Contain($"[{coluna}]");
    }
}
