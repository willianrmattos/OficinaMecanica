namespace OficinaMecanica.Application.Interfaces;

// Porta das mensagens de integracao entre servicos (tipos de OficinaMecanica.Contracts.*).
// Quem chama precisa publicar ANTES de IUnitOfWork.SaveChangesAsync: a implementacao grava a mensagem
// no outbox transacional, que so sai junto com o commit do SaveChanges. Publicar depois do commit
// perderia a mensagem se o processo caisse entre os dois passos.
public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(T mensagem, CancellationToken cancellationToken = default) where T : class;
}
