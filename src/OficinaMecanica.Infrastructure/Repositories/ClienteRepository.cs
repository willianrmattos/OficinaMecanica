using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Domain.ValueObjects;
using OficinaMecanica.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace OficinaMecanica.Infrastructure.Repositories;

public class ClienteRepository : IClienteRepository
{
    private readonly AppDbContext _context;

    public ClienteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Clientes
            .Include(c => c.Veiculos)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken cancellationToken = default)
    {
        return await _context.Clientes
            .FirstOrDefaultAsync(c => c.Documento.Numero == documento.Numero, cancellationToken);
    }

    public async Task<IEnumerable<Cliente>> ListarAsync(int pagina, int tamanhoPagina, string? filtroNome = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Clientes.Include(c => c.Veiculos).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtroNome))
            query = query.Where(c => c.Nome.Contains(filtroNome));

        return await query
            .OrderBy(c => c.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarAsync(string? filtroNome = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Clientes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filtroNome))
            query = query.Where(c => c.Nome.Contains(filtroNome));
        return await query.CountAsync(cancellationToken);
    }

    public async Task AdicionarAsync(Cliente cliente, CancellationToken cancellationToken = default)
    {
        await _context.Clientes.AddAsync(cliente, cancellationToken);
    }

    public void Atualizar(Cliente cliente)
    {
        _context.Clientes.Update(cliente);
    }

    public void Remover(Cliente cliente)
    {
        _context.Clientes.Remove(cliente);
    }
}
