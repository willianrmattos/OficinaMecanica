using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Application.Observabilidade;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OficinaMecanica.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task EnviarAsync(string destinatario, string assunto, string corpo, CancellationToken cancellationToken = default)
    {
        var smtpSettings = _configuration.GetSection("Smtp");
        var host = smtpSettings["Host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("Envio de e-mail ignorado: Smtp:Host não está configurado.");
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            // Parsing (porta, endereços) fica dentro do try de proposito: Cliente.Email nao tem
            // nenhuma validacao de formato no dominio, entao um e-mail mal formado (MailboxAddress.Parse)
            // ou uma porta invalida na configuracao nao pode virar excecao nao tratada aqui - isso
            // rodaria dentro do fluxo sincrono de qualquer endpoint que altera o status de uma OS.
            var port = int.Parse(smtpSettings["Port"] ?? "25");
            var remetente = smtpSettings["Remetente"] ?? "no-reply@oficinamecanica.local";

            var mensagem = new MimeMessage();
            mensagem.From.Add(MailboxAddress.Parse(remetente));
            mensagem.To.Add(MailboxAddress.Parse(destinatario));
            mensagem.Subject = assunto;
            mensagem.Body = new TextPart("plain") { Text = corpo };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.None, cts.Token);
            await client.SendAsync(mensagem, cts.Token);
            await client.DisconnectAsync(true, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar e-mail para {Destinatario} via SMTP {Host}.", destinatario, host);
            MetricasNegocio.EmailsFalha.Add(1, new KeyValuePair<string, object?>("host", host ?? "desconhecido"));
        }
    }
}
