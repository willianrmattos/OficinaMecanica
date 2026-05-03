namespace OficinaMecanica.Domain.Interfaces;

using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.ValueObjects;

public interface IVeiculoRepository
{
    Task<Veiculo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Veiculo?> ObterPorPlacaAsync(Placa placa, CancellationToken cancellationToken = default);
    Task<IEnumerable<Veiculo>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Veiculo veiculo, CancellationToken cancellationToken = default);
    void Atualizar(Veiculo veiculo);
    void Remover(Veiculo veiculo);
}
