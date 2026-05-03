using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace OficinaMecanica.Infrastructure.Repositories;

public class OrdemDeServicoRepository : IOrdemDeServicoRepository
{
    private readonly AppDbContext _context;

    public OrdemDeServicoRepository(AppDbContext context)
    {
        _context = context;
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

        if (filtroStatus.HasValue)
            query = query.Where(o => o.Status == filtroStatus.Value);

        return await query
            .OrderByDescending(o => o.DataAbertura)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarAsync(StatusOrdemDeServico? filtroStatus = null, CancellationToken cancellationToken = default)
    {
        var query = _context.OrdensDeServico.AsQueryable();
        if (filtroStatus.HasValue)
            query = query.Where(o => o.Status == filtroStatus.Value);
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

    public async Task AdicionarAsync(OrdemDeServico ordem, CancellationToken cancellationToken = default)
    {
        await _context.OrdensDeServico.AddAsync(ordem, cancellationToken);
    }

    public void Atualizar(OrdemDeServico ordem)
    {
        _context.OrdensDeServico.Update(ordem);
    }
}
