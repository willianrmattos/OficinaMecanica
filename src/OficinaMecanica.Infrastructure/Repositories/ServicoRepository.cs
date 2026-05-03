using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace OficinaMecanica.Infrastructure.Repositories;

public class ServicoRepository : IServicoRepository
{
    private readonly AppDbContext _context;

    public ServicoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Servico?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Servicos.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Servico>> ListarAsync(int pagina, int tamanhoPagina, bool apenasAtivos = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Servicos.AsQueryable();
        if (apenasAtivos) query = query.Where(s => s.Ativo);

        return await query
            .OrderBy(s => s.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarAsync(bool apenasAtivos = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Servicos.AsQueryable();
        if (apenasAtivos) query = query.Where(s => s.Ativo);
        return await query.CountAsync(cancellationToken);
    }

    public async Task AdicionarAsync(Servico servico, CancellationToken cancellationToken = default)
    {
        await _context.Servicos.AddAsync(servico, cancellationToken);
    }

    public void Atualizar(Servico servico)
    {
        _context.Servicos.Update(servico);
    }
}
