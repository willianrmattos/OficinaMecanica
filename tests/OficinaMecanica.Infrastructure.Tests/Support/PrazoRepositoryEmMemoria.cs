using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// IPrazoRepository sem SQL: o sweeper e testado contra ele com um relogio controlado. Imita o contrato da porta (reivindica os
// vencidos em ordem, marca, chama o callback e DESFAZ a marca se o callback falha, como a transacao real faria).
public sealed class PrazoRepositoryEmMemoria : IPrazoRepository
{
    private readonly object _trava = new();
    private readonly List<SagaPrazo> _prazos = new();

    // Falha injetada: lanca no inicio de ProcessarVencidosAsync (banco fora do ar).
    public Exception? FalharAoProcessar { get; set; }

    public List<(DateTimeOffset Agora, int Lote)> Chamadas { get; } = new();

    public IReadOnlyList<SagaPrazo> Prazos
    {
        get { lock (_trava) return _prazos.ToList(); }
    }

    public Task AdicionarAsync(SagaPrazo prazo, CancellationToken cancellationToken = default)
    {
        lock (_trava)
            _prazos.Add(prazo);

        return Task.CompletedTask;
    }

    public async Task<int> ProcessarVencidosAsync(
        DateTimeOffset agora, int lote, Func<SagaPrazo, CancellationToken, Task> aoExpirar, CancellationToken cancellationToken = default)
    {
        List<SagaPrazo> reivindicados;
        lock (_trava)
        {
            Chamadas.Add((agora, lote));

            if (FalharAoProcessar is not null)
                throw FalharAoProcessar;

            reivindicados = _prazos
                .Where(p => !p.Processado && p.ExpiraEm <= agora)
                .OrderBy(p => p.ExpiraEm).ThenBy(p => p.PrazoId)
                .Take(lote)
                .ToList();
            reivindicados.ForEach(p => p.MarcarProcessado());
        }

        try
        {
            foreach (var prazo in reivindicados)
                await aoExpirar(prazo, cancellationToken);
        }
        catch
        {
            // Rollback da transacao: as marcas voltam. SagaPrazo nao expoe "desmarcar" de proposito, entao o fake recria as linhas.
            lock (_trava)
            {
                foreach (var prazo in reivindicados)
                {
                    _prazos.Remove(prazo);
                    _prazos.Add(new SagaPrazo(prazo.CorrelationId, prazo.PrazoId, prazo.Tipo, prazo.ExpiraEm));
                }
            }

            throw;
        }

        return reivindicados.Count;
    }
}

// Captura o que o sweeper publica, no lugar do outbox.
public sealed class PublicadorDeTeste : IIntegrationEventPublisher
{
    private readonly List<object> _publicadas = new();

    public Exception? FalharAoPublicar { get; set; }

    public IReadOnlyList<object> Publicadas
    {
        get { lock (_publicadas) return _publicadas.ToList(); }
    }

    public Task PublishAsync<T>(T mensagem, CancellationToken cancellationToken = default) where T : class
    {
        if (FalharAoPublicar is not null)
            throw FalharAoPublicar;

        lock (_publicadas)
            _publicadas.Add(mensagem);

        return Task.CompletedTask;
    }
}
