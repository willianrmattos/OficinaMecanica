namespace OficinaMecanica.Application.Interfaces;

public interface IEmailService
{
    Task EnviarAsync(string destinatario, string assunto, string corpo, CancellationToken cancellationToken = default);
}
