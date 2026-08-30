namespace OficinaMecanica.Domain.Interfaces;

using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;

public interface IOrdemDeServicoRepository
{
    Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrdemDeServico?> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrdemDeServico>> ListarAsync(int pagina, int tamanhoPagina, StatusOrdemDeServico? filtroStatus = null, CancellationToken cancellationToken = default);
    Task<int> ContarAsync(StatusOrdemDeServico? filtroStatus = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrdemDeServico>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default);
    Task<double> ObterTempoMedioExecucaoAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<StatusOrdemDeServico, double>> ObterTempoMedioPorStatusAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(OrdemDeServico ordem, CancellationToken cancellationToken = default);
    void Atualizar(OrdemDeServico ordem);
}
