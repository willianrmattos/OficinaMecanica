using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class ListarOrdensDeServicoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ListarOrdensDeServicoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> ObterTokenAsync()
    {
        return await Task.FromResult(TestTokenFactory.GerarToken(_factory));
    }

    [Fact]
    public async Task Listar_SemFiltroStatus_DeveExcluirFinalizadasEEntreguesEOrdenarPorPrioridade()
    {
        var recebidaAntiga = new OrdemDeServicoBuilder().Recebida();
        var recebidaNova = new OrdemDeServicoBuilder().Recebida();
        var emExecucao = new OrdemDeServicoBuilder().EmExecucao();
        var finalizada = new OrdemDeServicoBuilder().Finalizada();
        var entregue = new OrdemDeServicoBuilder().Entregue();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.OrdensDeServico.AddRange(recebidaAntiga, recebidaNova, emExecucao, finalizada, entregue);
            await db.SaveChangesAsync();

            db.Entry(recebidaAntiga).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-5);
            db.Entry(recebidaNova).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-1);
            db.Entry(emExecucao).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-3);
            await db.SaveChangesAsync();
        }

        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/ordens-de-servico?pagina=1&tamanhoPagina=50");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var itens = json.RootElement.GetProperty("itens").EnumerateArray().ToList();

        var statusPorId = itens.ToDictionary(
            i => i.GetProperty("id").GetGuid(),
            i => i.GetProperty("status").GetString());

        // Nenhuma OS Finalizada ou Entregue deve aparecer na listagem padrão (sem filtroStatus).
        statusPorId.Values.Should().NotContain("Finalizada");
        statusPorId.Values.Should().NotContain("Entregue");
        statusPorId.Should().NotContainKey(finalizada.Id);
        statusPorId.Should().NotContainKey(entregue.Id);

        // As demais devem aparecer.
        statusPorId.Should().ContainKey(recebidaAntiga.Id);
        statusPorId.Should().ContainKey(recebidaNova.Id);
        statusPorId.Should().ContainKey(emExecucao.Id);

        var ids = itens.Select(i => i.GetProperty("id").GetGuid()).ToList();
        var indiceEmExecucao = ids.IndexOf(emExecucao.Id);
        var indiceRecebidaAntiga = ids.IndexOf(recebidaAntiga.Id);
        var indiceRecebidaNova = ids.IndexOf(recebidaNova.Id);

        // EmExecucao tem prioridade maior que Recebida, deve vir antes.
        indiceEmExecucao.Should().BeLessThan(indiceRecebidaAntiga);
        indiceEmExecucao.Should().BeLessThan(indiceRecebidaNova);

        // Dentro do mesmo status (Recebida), a mais antiga vem primeiro.
        indiceRecebidaAntiga.Should().BeLessThan(indiceRecebidaNova);
    }

    [Fact]
    public async Task Listar_SemFiltroStatus_DeveSeguirAPrioridadeExplicitaIncluindoPagamentoECancelada()
    {
        var canceladaAntiga = new OrdemDeServicoBuilder().Cancelada();
        var recusadaAntiga = new OrdemDeServicoBuilder().OrcamentoRecusado();
        var canceladaNova = new OrdemDeServicoBuilder().Cancelada();
        var recusadaNova = new OrdemDeServicoBuilder().OrcamentoRecusado();
        var recebida = new OrdemDeServicoBuilder().Recebida();
        var emDiagnostico = new OrdemDeServicoBuilder().EmDiagnostico();
        var aguardandoAprovacao = new OrdemDeServicoBuilder().AguardandoAprovacao();
        var aguardandoPagamento = new OrdemDeServicoBuilder().AguardandoPagamento();
        var emExecucao = new OrdemDeServicoBuilder().EmExecucao();
        var finalizada = new OrdemDeServicoBuilder().Finalizada();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OrdensDeServico.AddRange(
                canceladaAntiga, recusadaAntiga, canceladaNova, recusadaNova, recebida, emDiagnostico,
                aguardandoAprovacao, aguardandoPagamento, emExecucao, finalizada);
            await db.SaveChangesAsync();

            db.Entry(canceladaAntiga).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-12);
            db.Entry(recusadaAntiga).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-9);
            db.Entry(canceladaNova).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-2);
            db.Entry(recusadaNova).Property(nameof(OrdemDeServico.DataAbertura)).CurrentValue = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ObterTokenAsync());

        var response = await _client.GetAsync("/api/ordens-de-servico?pagina=1&tamanhoPagina=50");
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var idsNaOrdemRetornada = json.RootElement.GetProperty("itens").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())
            .ToList();

        var semeadas = new[]
        {
            emExecucao.Id, aguardandoPagamento.Id, aguardandoAprovacao.Id, emDiagnostico.Id, recebida.Id,
            canceladaAntiga.Id, recusadaAntiga.Id, canceladaNova.Id, recusadaNova.Id
        };

        // Cancelada (valor 8 no enum) nao pode subir para o topo: a prioridade e explicita. OrcamentoRecusado e
        // Cancelada empatam no fim, entao as quatro se intercalam pela data de abertura (mais antiga primeiro),
        // sem uma das duas classes ficar sempre acima da outra.
        idsNaOrdemRetornada.Where(semeadas.Contains).Should().Equal(semeadas);
        idsNaOrdemRetornada.Should().NotContain(finalizada.Id);
    }

    [Fact]
    public async Task Listar_HistoricoGravadoForaDeOrdem_DeveRetornarCadaOrdemComOHistoricoEmOrdemCronologica()
    {
        var inicio = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        var emExecucao = new OrdemDeServicoBuilder().EmExecucao().ComHistoricoEmOrdemInversa(inicio);
        var aguardandoAprovacao = new OrdemDeServicoBuilder().AguardandoAprovacao().ComHistoricoEmOrdemInversa(inicio, mesmaData: true);
        await _factory.SemearAsync(emExecucao, aguardandoAprovacao);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ObterTokenAsync());

        var response = await _client.GetAsync("/api/ordens-de-servico?pagina=1&tamanhoPagina=50");
        response.EnsureSuccessStatusCode();

        var itens = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("itens").EnumerateArray().ToList();
        List<string?> HistoricoDe(Guid id) => itens.Single(i => i.GetProperty("id").GetGuid() == id)
            .GetProperty("historicoStatus").EnumerateArray().Select(h => h.GetProperty("status").GetString()).ToList();

        HistoricoDe(emExecucao.Id).Should().Equal("Recebida", "EmDiagnostico", "AguardandoAprovacao", "AguardandoPagamento", "EmExecucao");
        HistoricoDe(aguardandoAprovacao.Id).Should().Equal("Recebida", "EmDiagnostico", "AguardandoAprovacao");
    }

    [Fact]
    public async Task Listar_ComFiltroStatusCancelada_DeveRetornarSoCanceladas()
    {
        var cancelada = new OrdemDeServicoBuilder().Cancelada();
        var recebida = new OrdemDeServicoBuilder().Recebida();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OrdensDeServico.AddRange(cancelada, recebida);
            await db.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ObterTokenAsync());

        var response = await _client.GetAsync("/api/ordens-de-servico?pagina=1&tamanhoPagina=50&filtroStatus=Cancelada");
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var itens = json.RootElement.GetProperty("itens").EnumerateArray().ToList();

        itens.Select(i => i.GetProperty("id").GetGuid()).Should().Contain(cancelada.Id).And.NotContain(recebida.Id);
        itens.Should().OnlyContain(i => i.GetProperty("status").GetString() == "Cancelada");
    }

    [Fact]
    public async Task Listar_ComFiltroStatusFinalizada_DeveRetornarFinalizadas()
    {
        var finalizada = new OrdemDeServicoBuilder().Finalizada();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OrdensDeServico.Add(finalizada);
            await db.SaveChangesAsync();
        }

        var token = await ObterTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/ordens-de-servico?pagina=1&tamanhoPagina=50&filtroStatus=Finalizada");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var itens = json.RootElement.GetProperty("itens").EnumerateArray().ToList();

        var ids = itens.Select(i => i.GetProperty("id").GetGuid()).ToList();

        // Filtro explícito sobrepõe a exclusão padrão: a OS finalizada deve aparecer.
        ids.Should().Contain(finalizada.Id);
        itens.Should().OnlyContain(i => i.GetProperty("status").GetString() == "Finalizada");
    }
}
