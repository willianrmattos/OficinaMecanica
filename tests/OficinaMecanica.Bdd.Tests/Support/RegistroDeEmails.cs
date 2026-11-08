using System.Collections.Concurrent;
using OficinaMecanica.Application.Interfaces;

namespace OficinaMecanica.Bdd.Tests.Support;

// Substitui o SMTP: guarda cada e-mail "enviado" para os cenarios afirmarem o que o cliente recebeu.
public sealed class RegistroDeEmails : IEmailService
{
    private readonly ConcurrentQueue<EmailEnviado> _enviados = new();

    public IReadOnlyList<EmailEnviado> Enviados => _enviados.ToArray();

    public Task EnviarAsync(string destinatario, string assunto, string corpo, CancellationToken cancellationToken = default)
    {
        _enviados.Enqueue(new EmailEnviado(destinatario, assunto, corpo));
        return Task.CompletedTask;
    }
}

public sealed record EmailEnviado(string Destinatario, string Assunto, string Corpo);
