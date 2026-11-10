using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// Contra SQL Server de verdade porque a ordem em que o banco entrega as linhas do historico e comportamento do provider relacional
// (o InMemory devolve na ordem de insercao e esconderia o defeito): a chave do historico e um Guid aleatorio, entao, sem ORDER BY,
// o indice devolve as linhas na ordem do Guid, que nao tem relacao com o tempo. Aqui as chaves sao escolhidas para que a ordem do
// indice seja o inverso da cronologica, o pior caso, e cada leitura do repositorio precisa devolver o historico em ordem do tempo.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class HistoricoDeStatusComSqlTests
{
    private static readonly DateTime Inicio = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly InfraFixture _infra;

    public HistoricoDeStatusComSqlTests(InfraFixture infra) => _infra = infra;

    private async Task<ServiceProvider> CriarProvedorMigradoAsync()
    {
        var configuracao = ConfiguracaoDeTeste.Criar(("ConnectionStrings:DefaultConnection", await _infra.CriarBancoAsync()));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuracao);
        var provedor = services.BuildServiceProvider(validateScopes: true);

        await using var escopo = provedor.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        return provedor;
    }

    // Refaz o historico da OS com uma linha por status do fluxo, uma hora de distancia entre elas (ou todas na mesma data) e chaves
    // que crescem do fim para o comeco do fluxo: a primeira transicao fica com a chave de maior posicao no indice.
    private static OrdemDeServico ComHistoricoInvertidoNoIndice(OrdemDeServico ordem, int deslocamentoDeChave = 0, bool mesmaData = false)
    {
        var fluxo = ordem.HistoricoStatus.Select(h => h.Status).ToList();
        var linhas = fluxo
            .Select((status, i) => HistoricoEmbaralhado.Linha(
                ordem.Id, status, mesmaData ? Inicio : Inicio.AddHours(i),
                HistoricoEmbaralhado.ChaveNaPosicao(deslocamentoDeChave + fluxo.Count - i)))
            .ToArray();

        ordem.ComHistoricoNaOrdem(linhas);
        ordem.ClearDomainEvents();
        return ordem;
    }

    private static async Task SemearAsync(ServiceProvider provedor, params OrdemDeServico[] ordens)
    {
        await using var escopo = provedor.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(ordens);
        await db.SaveChangesAsync();
    }

    private static async Task<T> LerAsync<T>(ServiceProvider provedor, Func<IOrdemDeServicoRepository, Task<T>> leitura)
    {
        await using var escopo = provedor.CreateAsyncScope();
        return await leitura(escopo.ServiceProvider.GetRequiredService<IOrdemDeServicoRepository>());
    }

    // A premissa do teste: lido pela chave, que e a ordem que o indice entrega sem ORDER BY, o historico sai do fim para o comeco.
    private static async Task<List<StatusOrdemDeServico>> StatusNaOrdemDoIndiceAsync(ServiceProvider provedor, Guid ordemId)
    {
        await using var escopo = provedor.CreateAsyncScope();
        var conexao = escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await conexao.OpenAsync();
        await using var comando = (SqlCommand)conexao.CreateCommand();
        comando.CommandText = "SELECT Status FROM HistoricoStatus WHERE OrdemDeServicoId = @id ORDER BY Id";
        comando.Parameters.AddWithValue("@id", ordemId);
        await using var leitor = await comando.ExecuteReaderAsync();

        var status = new List<StatusOrdemDeServico>();
        while (await leitor.ReadAsync())
            status.Add((StatusOrdemDeServico)leitor.GetInt32(0));
        return status;
    }

    private static readonly StatusOrdemDeServico[] FluxoDaOsFinalizada =
    {
        StatusOrdemDeServico.Recebida, StatusOrdemDeServico.EmDiagnostico, StatusOrdemDeServico.AguardandoAprovacao,
        StatusOrdemDeServico.AguardandoPagamento, StatusOrdemDeServico.EmExecucao, StatusOrdemDeServico.Finalizada
    };

    [Fact]
    public async Task ObterPorId_HistoricoComChavesNaOrdemInversaDoTempo_DeveDevolverEmOrdemCronologica()
    {
        await using var provedor = await CriarProvedorMigradoAsync();
        var ordem = ComHistoricoInvertidoNoIndice(new OrdemDeServicoBuilder().Finalizada());
        await SemearAsync(provedor, ordem);
        (await StatusNaOrdemDoIndiceAsync(provedor, ordem.Id)).Should().Equal(FluxoDaOsFinalizada.Reverse(), "e a ordem que o banco entrega sem ORDER BY");

        var lida = await LerAsync(provedor, r => r.ObterPorIdAsync(ordem.Id));

        lida!.HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoDaOsFinalizada);
        lida.HistoricoStatus.Select(h => h.DataAlteracao).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ObterPorNumero_HistoricoComChavesNaOrdemInversaDoTempo_DeveDevolverEmOrdemCronologica()
    {
        await using var provedor = await CriarProvedorMigradoAsync();
        var ordem = ComHistoricoInvertidoNoIndice(new OrdemDeServicoBuilder().Finalizada());
        await SemearAsync(provedor, ordem);

        var lida = await LerAsync(provedor, r => r.ObterPorNumeroAsync(ordem.Numero));

        lida!.HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoDaOsFinalizada);
    }

    [Fact]
    public async Task ObterPorId_TransicoesNoMesmoInstante_DeveDesempatarPelaPosicaoNoFluxo()
    {
        await using var provedor = await CriarProvedorMigradoAsync();
        var ordem = ComHistoricoInvertidoNoIndice(new OrdemDeServicoBuilder().Finalizada(), mesmaData: true);
        await SemearAsync(provedor, ordem);
        (await StatusNaOrdemDoIndiceAsync(provedor, ordem.Id)).Should().Equal(FluxoDaOsFinalizada.Reverse());

        var lida = await LerAsync(provedor, r => r.ObterPorIdAsync(ordem.Id));

        lida!.HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoDaOsFinalizada);
    }

    [Fact]
    public async Task ListarPorCliente_HistoricoComChavesNaOrdemInversaDoTempo_DeveDevolverCadaOrdemEmOrdemCronologica()
    {
        await using var provedor = await CriarProvedorMigradoAsync();
        var clienteId = Guid.NewGuid();
        var primeira = ComHistoricoInvertidoNoIndice(new OrdemDeServicoBuilder().DoCliente(clienteId).Finalizada(), deslocamentoDeChave: 0);
        var segunda = ComHistoricoInvertidoNoIndice(new OrdemDeServicoBuilder().DoCliente(clienteId).EmExecucao(), deslocamentoDeChave: 100);
        await SemearAsync(provedor, primeira, segunda);

        var lidas = (await LerAsync(provedor, r => r.ListarPorClienteAsync(clienteId))).ToList();

        lidas.Should().HaveCount(2);
        lidas.Single(o => o.Id == primeira.Id).HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoDaOsFinalizada);
        lidas.Single(o => o.Id == segunda.Id).HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoDaOsFinalizada.Take(5));
    }

    [Fact]
    public async Task Listar_HistoricoComChavesNaOrdemInversaDoTempo_DeveManterAPrioridadeDoStatusEOrdenarOHistoricoDeCadaOrdem()
    {
        await using var provedor = await CriarProvedorMigradoAsync();
        var recebida = ComHistoricoInvertidoNoIndice(new OrdemDeServicoBuilder().Recebida(), deslocamentoDeChave: 0);
        var emExecucao = ComHistoricoInvertidoNoIndice(new OrdemDeServicoBuilder().EmExecucao(), deslocamentoDeChave: 100);
        var aguardandoAprovacao = ComHistoricoInvertidoNoIndice(new OrdemDeServicoBuilder().AguardandoAprovacao(), deslocamentoDeChave: 200);
        await SemearAsync(provedor, recebida, emExecucao, aguardandoAprovacao);

        var pagina = (await LerAsync(provedor, r => r.ListarAsync(1, 10))).ToList();

        pagina.Select(o => o.Status).Should().Equal(
            StatusOrdemDeServico.EmExecucao, StatusOrdemDeServico.AguardandoAprovacao, StatusOrdemDeServico.Recebida);
        pagina[0].HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoDaOsFinalizada.Take(5));
        pagina[1].HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoDaOsFinalizada.Take(3));
        pagina[2].HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoDaOsFinalizada.Take(1));

        var segundaPagina = (await LerAsync(provedor, r => r.ListarAsync(2, 1))).ToList();

        segundaPagina.Should().ContainSingle().Which.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        segundaPagina.Single().HistoricoStatus.Select(h => h.Status).Should().Equal(FluxoDaOsFinalizada.Take(3));
    }
}
