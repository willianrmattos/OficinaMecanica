using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace OficinaMecanica.Infrastructure.Repositories;

public class OrdemDeServicoRepository : IOrdemDeServicoRepository
{
    private readonly AppDbContext _context;

    private static readonly StatusOrdemDeServico[] StatusExcluidosPorPadrao =
    {
        StatusOrdemDeServico.Finalizada,
        StatusOrdemDeServico.Entregue
    };

    // Prioridade explicita em vez de ordenar pelo valor do enum: os valores numericos nao podem ser
    // renumerados (estao persistidos e viajam em mensagens), entao AguardandoPagamento (7) e Cancelada (8)
    // nao ficam no lugar certo se a ordem vier do numero. Uma cadeia de condicionais vira CASE no SQL Server
    // e tambem roda no provider InMemory dos testes. OrcamentoRecusado e Cancelada empatam no fim.
    private static readonly Expression<Func<OrdemDeServico, int>> PrioridadeDoStatus = o =>
        o.Status == StatusOrdemDeServico.EmExecucao ? 5
        : o.Status == StatusOrdemDeServico.AguardandoPagamento ? 4
        : o.Status == StatusOrdemDeServico.AguardandoAprovacao ? 3
        : o.Status == StatusOrdemDeServico.EmDiagnostico ? 2
        : o.Status == StatusOrdemDeServico.Recebida ? 1
        : 0;

    public OrdemDeServicoRepository(AppDbContext context)
    {
        _context = context;
    }

    private static IQueryable<OrdemDeServico> AplicarFiltroStatus(IQueryable<OrdemDeServico> query, StatusOrdemDeServico? filtroStatus)
    {
        if (filtroStatus.HasValue)
            return query.Where(o => o.Status == filtroStatus.Value);

        return query.Where(o => !StatusExcluidosPorPadrao.Contains(o.Status));
    }

    public async Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OrdensDeServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .Include(o => o.HistoricoStatus)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<OrdemDeServico?> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default)
    {
        return await _context.OrdensDeServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .Include(o => o.HistoricoStatus)
            .FirstOrDefaultAsync(o => o.Numero == numero, cancellationToken);
    }

    public async Task<IEnumerable<OrdemDeServico>> ListarAsync(int pagina, int tamanhoPagina, StatusOrdemDeServico? filtroStatus = null, CancellationToken cancellationToken = default)
    {
        var query = _context.OrdensDeServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .Include(o => o.HistoricoStatus)
            .AsQueryable();

        query = AplicarFiltroStatus(query, filtroStatus);

        return await query
            .OrderByDescending(PrioridadeDoStatus)
            .ThenBy(o => o.DataAbertura)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarAsync(StatusOrdemDeServico? filtroStatus = null, CancellationToken cancellationToken = default)
    {
        var query = _context.OrdensDeServico.AsQueryable();
        query = AplicarFiltroStatus(query, filtroStatus);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<IEnumerable<OrdemDeServico>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default)
    {
        return await _context.OrdensDeServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .Include(o => o.HistoricoStatus)
            .Where(o => o.ClienteId == clienteId)
            .OrderByDescending(o => o.DataAbertura)
            .ToListAsync(cancellationToken);
    }

    public async Task<double> ObterTempoMedioExecucaoAsync(CancellationToken cancellationToken = default)
    {
        var ordensFinalizadas = await _context.OrdensDeServico
            .Where(o => o.DataConclusao != null)
            .Select(o => new { o.DataAbertura, o.DataConclusao })
            .ToListAsync(cancellationToken);

        if (!ordensFinalizadas.Any()) return 0;

        return ordensFinalizadas
            .Average(o => (o.DataConclusao!.Value - o.DataAbertura).TotalHours);
    }

    public async Task<IReadOnlyDictionary<StatusOrdemDeServico, double>> ObterTempoMedioPorStatusAsync(CancellationToken cancellationToken = default)
    {
        var historico = await _context.HistoricoStatus
            .Select(h => new { h.OrdemDeServicoId, h.Status, h.DataAlteracao })
            .ToListAsync(cancellationToken);

        // Duracao de cada status = intervalo ate a proxima mudanca de status da mesma OS
        // (o ultimo status de cada OS fica sem par - ainda nao "saiu" desse status).
        return historico
            .GroupBy(h => h.OrdemDeServicoId)
            .SelectMany(porOrdem =>
            {
                var ordenado = porOrdem.OrderBy(h => h.DataAlteracao).ToList();
                return ordenado
                    .Zip(ordenado.Skip(1), (atual, proximo) => new
                    {
                        atual.Status,
                        Duracao = (proximo.DataAlteracao - atual.DataAlteracao).TotalHours
                    });
            })
            .GroupBy(d => d.Status)
            .ToDictionary(g => g.Key, g => g.Average(d => d.Duracao));
    }

    public async Task AdicionarAsync(OrdemDeServico ordem, CancellationToken cancellationToken = default)
    {
        await _context.OrdensDeServico.AddAsync(ordem, cancellationToken);
    }

    public void Atualizar(OrdemDeServico ordem)
    {
        _context.OrdensDeServico.Update(ordem);
    }
}
