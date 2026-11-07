using System.Collections.Concurrent;
using OficinaMecanica.Application.Interfaces;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// Substitui o SMTP: guarda cada e-mail "enviado" para os testes contarem quantos saíram e quando.
public sealed class RegistroDeEmails : IEmailService
{
    private readonly ConcurrentQueue<EmailEnviado> _enviados = new();

    public IReadOnlyList<EmailEnviado> Enviados => _enviados.ToArray();

    // Liga a falha do "SMTP" para provar que um handler que lanca depois do commit nao desfaz a transacao.
    public bool FalharAoEnviar { get; set; }

    public Task EnviarAsync(string destinatario, string assunto, string corpo, CancellationToken cancellationToken = default)
    {
        if (FalharAoEnviar)
            throw new FalhaTecnicaSimuladaException();

        _enviados.Enqueue(new EmailEnviado(destinatario, assunto, corpo));
        return Task.CompletedTask;
    }
}

public sealed record EmailEnviado(string Destinatario, string Assunto, string Corpo);
