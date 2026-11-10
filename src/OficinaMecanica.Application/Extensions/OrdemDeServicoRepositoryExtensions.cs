using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Extensions;

public static class OrdemDeServicoRepositoryExtensions
{
    public static async Task<OrdemDeServico> ObterObrigatoriaAsync(
        this IOrdemDeServicoRepository repositorio, Guid id, CancellationToken cancellationToken = default)
    {
        return await repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new DomainException("Ordem de serviço não encontrada.");
    }
}
