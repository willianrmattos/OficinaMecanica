using System.Collections.Concurrent;
using OficinaMecanica.Application.Interfaces;

namespace OficinaMecanica.Integration.Tests.Fakes;

public sealed class FakeIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ConcurrentQueue<object> _mensagens = new();

    public IReadOnlyCollection<object> Mensagens => _mensagens.ToArray();

    public IReadOnlyList<T> Publicadas<T>() where T : class => _mensagens.OfType<T>().ToList();

    public Task PublishAsync<T>(T mensagem, CancellationToken cancellationToken = default) where T : class
    {
        _mensagens.Enqueue(mensagem);
        return Task.CompletedTask;
    }
}
