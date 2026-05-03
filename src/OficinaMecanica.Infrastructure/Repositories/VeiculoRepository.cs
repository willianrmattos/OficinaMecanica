using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Domain.ValueObjects;
using OficinaMecanica.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace OficinaMecanica.Infrastructure.Repositories;

public class VeiculoRepository : IVeiculoRepository
{
    private readonly AppDbContext _context;

    public VeiculoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Veiculo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Veiculos.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Veiculo?> ObterPorPlacaAsync(Placa placa, CancellationToken cancellationToken = default)
    {
        return await _context.Veiculos
            .FirstOrDefaultAsync(v => v.Placa.Valor == placa.Valor, cancellationToken);
    }

    public async Task<IEnumerable<Veiculo>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default)
    {
        return await _context.Veiculos
            .Where(v => v.ClienteId == clienteId)
            .OrderBy(v => v.Marca)
            .ToListAsync(cancellationToken);
    }

    public async Task AdicionarAsync(Veiculo veiculo, CancellationToken cancellationToken = default)
    {
        await _context.Veiculos.AddAsync(veiculo, cancellationToken);
    }

    public void Atualizar(Veiculo veiculo)
    {
        _context.Veiculos.Update(veiculo);
    }

    public void Remover(Veiculo veiculo)
    {
        _context.Veiculos.Remove(veiculo);
    }
}
