using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Infrastructure.Saga;

// Le a linha da saga (OrdemDeServicoSagas) do proprio banco do OS Service, so para consulta: sem rastrear a entidade e sem
// nenhuma escrita, entao nunca disputa o bloqueio que o MassTransit pede ao processar um evento da mesma OS.
public sealed class SagaStateReader : ISagaStateReader
{
    private readonly AppDbContext _db;

    public SagaStateReader(AppDbContext db)
    {
        _db = db;
    }

    public Task<EstadoConhecidoDaSaga?> ObterAsync(Guid ordemDeServicoId, CancellationToken cancellationToken = default) =>
        _db.Set<OrdemDeServicoSagaState>()
            .AsNoTracking()
            .Where(saga => saga.CorrelationId == ordemDeServicoId)
            .Select(saga => new EstadoConhecidoDaSaga(saga.CurrentState, saga.AtualizadaEm))
            .SingleOrDefaultAsync(cancellationToken);
}
