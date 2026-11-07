using MassTransit;
using OficinaMecanica.Application.Interfaces;

namespace OficinaMecanica.Infrastructure.Messaging;

// Implementacao da porta de publicacao sobre o MassTransit. Com o bus outbox ligado, o IPublishEndpoint do escopo
// so grava a mensagem no AppDbContext e ela sai do banco junto com o SaveChanges do handler (ou do consumer).
// O MessageId do contrato e copiado para o do envelope porque e ele que o inbox de quem consome usa para
// deduplicar: se o envelope ganhasse um id novo a cada publicacao, a mesma mensagem reenviada passaria como
// duas e o consumer rodaria duas vezes.
public sealed class MassTransitIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<T>(T mensagem, CancellationToken cancellationToken = default) where T : class =>
        MessageIdDoContrato<T>.Obter(mensagem) is { } messageId
            ? _publishEndpoint.Publish(mensagem, Pipe.Execute<PublishContext<T>>(contexto => contexto.MessageId = messageId), cancellationToken)
            : _publishEndpoint.Publish(mensagem, cancellationToken);
}
