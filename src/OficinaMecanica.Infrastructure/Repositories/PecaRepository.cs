using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace OficinaMecanica.Infrastructure.Repositories;

public class PecaRepository : IPecaRepository
{
    private readonly AppDbContext _context;

    public PecaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Pecas.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Peca>> ListarAsync(int pagina, int tamanhoPagina, bool apenasAtivos = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Pecas.AsQueryable();
        if (apenasAtivos) query = query.Where(p => p.Ativo);

        return await query
            .OrderBy(p => p.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarAsync(bool apenasAtivos = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Pecas.AsQueryable();
        if (apenasAtivos) query = query.Where(p => p.Ativo);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<IEnumerable<Peca>> ListarComEstoqueBaixoAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Pecas
            .Where(p => p.Ativo && p.QuantidadeEstoque < p.EstoqueMinimo)
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task AdicionarAsync(Peca peca, CancellationToken cancellationToken = default)
    {
        await _context.Pecas.AddAsync(peca, cancellationToken);
    }

    public void Atualizar(Peca peca)
    {
        _context.Pecas.Update(peca);
    }
}
