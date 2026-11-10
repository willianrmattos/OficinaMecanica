using Microsoft.Extensions.Options;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Infrastructure.Saga;

// Uma varredura: reivindica os prazos vencidos e publica PrazoExpirado para cada um. A publicacao vai pelo outbox
// (IIntegrationEventPublisher) e o SaveChanges do repositorio a grava junto com a marca "processado": publicar e marcar
// comitam juntos, entao nenhum prazo some sem aviso nem avisa duas vezes. O sweeper nunca escreve na linha da saga: quem
// decide o que o prazo significa e a saga, ao consumir o evento.
public sealed class SweeperDePrazos
{
    private readonly IPrazoRepository _prazos;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly TimeProvider _relogio;
    private readonly SagaOptions _opcoes;

    public SweeperDePrazos(
        IPrazoRepository prazos,
        IIntegrationEventPublisher publisher,
        TimeProvider relogio,
        IOptions<SagaOptions> opcoes)
    {
        _prazos = prazos;
        _publisher = publisher;
        _relogio = relogio;
        _opcoes = opcoes.Value;
    }

    public Task<int> ProcessarLoteAsync(CancellationToken cancellationToken = default) =>
        _prazos.ProcessarVencidosAsync(
            _relogio.GetUtcNow(),
            _opcoes.SweeperLote,
            (prazo, ct) => _publisher.PublishAsync(
                new PrazoExpirado(
                    // Id derivado do prazo: a reentrega do outbox leva o mesmo MessageId e o inbox da saga a descarta.
                    IdentificadorDeMensagem.Para(prazo.CorrelationId, nameof(PrazoExpirado), prazo.PrazoId),
                    prazo.CorrelationId,
                    _relogio.GetUtcNow(),
                    prazo.PrazoId,
                    prazo.Tipo),
                ct),
            cancellationToken);
}
