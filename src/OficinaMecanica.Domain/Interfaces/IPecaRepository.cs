namespace OficinaMecanica.Domain.Interfaces;

using OficinaMecanica.Domain.Entities;

public interface IPecaRepository
{
    Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Peca>> ListarAsync(int pagina, int tamanhoPagina, bool apenasAtivos = true, CancellationToken cancellationToken = default);
    Task<int> ContarAsync(bool apenasAtivos = true, CancellationToken cancellationToken = default);
    Task<IEnumerable<Peca>> ListarComEstoqueBaixoAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(Peca peca, CancellationToken cancellationToken = default);
    void Atualizar(Peca peca);
}
