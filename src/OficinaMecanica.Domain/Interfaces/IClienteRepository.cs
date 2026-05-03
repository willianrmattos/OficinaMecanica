namespace OficinaMecanica.Domain.Interfaces;

using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.ValueObjects;

public interface IClienteRepository
{
    Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken cancellationToken = default);
    Task<IEnumerable<Cliente>> ListarAsync(int pagina, int tamanhoPagina, string? filtroNome = null, CancellationToken cancellationToken = default);
    Task<int> ContarAsync(string? filtroNome = null, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Cliente cliente, CancellationToken cancellationToken = default);
    void Atualizar(Cliente cliente);
    void Remover(Cliente cliente);
}
