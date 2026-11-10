using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Infrastructure.Saga;

// Le do proprio banco do OS Service (nunca o de outro servico) quantas sagas ha em cada estado e quantas, nos estados de
// compensacao, estao paradas alem do limite, e entrega o retrato aos gauges de MetricasDaSaga. Quem o chama e o servico de
// metricas de negocio que ja existe (a mesma coleta periodica do tempo medio por status), entao nao ha tarefa de fundo nova.
// Sao duas consultas agregadas sobre uma tabela com indice em CurrentState: custo desprezivel no intervalo de minutos.
public sealed class ColetorDeMetricasDaSaga
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _relogio;
    private readonly SagaOptions _opcoes;

    public ColetorDeMetricasDaSaga(AppDbContext db, TimeProvider relogio, IOptions<SagaOptions> opcoes)
    {
        _db = db;
        _relogio = relogio;
        _opcoes = opcoes.Value;
    }

    public async Task ColetarAsync(CancellationToken cancellationToken = default)
    {
        var sagas = _db.Set<OrdemDeServicoSagaState>().AsNoTracking();

        var porEstado = await sagas
            .GroupBy(saga => saga.CurrentState)
            .Select(grupo => new { Estado = grupo.Key, Total = grupo.Count() })
            .ToListAsync(cancellationToken);

        var limite = _relogio.GetUtcNow().AddMinutes(-_opcoes.CompensacaoPresaMinutos);
        var estadosDeCompensacao = MetricasDaSaga.EstadosDeCompensacao;

        var presas = await sagas
            .Where(saga => estadosDeCompensacao.Contains(saga.CurrentState) && saga.AtualizadaEm < limite)
            .GroupBy(saga => saga.CurrentState)
            .Select(grupo => new { Estado = grupo.Key, Total = grupo.Count() })
            .ToListAsync(cancellationToken);

        // Todo estado de compensacao aparece no gauge, com zero quando nao ha saga presa: o alerta precisa enxergar o zero para
        // fechar, e a ausencia do ponto nao distingue "nenhuma presa" de "coleta parada".
        var compensacaoPresa = estadosDeCompensacao.ToDictionary(
            estado => estado,
            estado => presas.Where(p => p.Estado == estado).Select(p => (long)p.Total).SingleOrDefault());

        MetricasDaSaga.Atualizar(new InstantaneoDasSagas(
            porEstado.ToDictionary(p => p.Estado, p => (long)p.Total),
            compensacaoPresa));
    }
}
