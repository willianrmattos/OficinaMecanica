namespace OficinaMecanica.Domain.Interfaces;

using OficinaMecanica.Domain.Entities;

public interface IServicoRepository
{
    Task<Servico?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Servico>> ListarAsync(int pagina, int tamanhoPagina, bool apenasAtivos = true, CancellationToken cancellationToken = default);
    Task<int> ContarAsync(bool apenasAtivos = true, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Servico servico, CancellationToken cancellationToken = default);
    void Atualizar(Servico servico);
}
