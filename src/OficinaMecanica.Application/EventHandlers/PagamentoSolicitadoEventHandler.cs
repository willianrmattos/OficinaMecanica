using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.EventHandlers;

public class PagamentoSolicitadoEventHandler : INotificationHandler<PagamentoSolicitadoEvent>
{
    // Optei por um offset fixo em vez de TimeZoneInfo: o Brasil nao tem horario de verao desde 2019 e assim
    // o e-mail nao depende de o container ter o banco de fusos horarios instalado.
    private static readonly TimeSpan OffsetDeBrasilia = TimeSpan.FromHours(-3);

    private readonly IClienteRepository _clienteRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<PagamentoSolicitadoEventHandler> _logger;

    public PagamentoSolicitadoEventHandler(
        IClienteRepository clienteRepository,
        IEmailService emailService,
        ILogger<PagamentoSolicitadoEventHandler> logger)
    {
        _clienteRepository = clienteRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(PagamentoSolicitadoEvent notification, CancellationToken cancellationToken)
    {
        var cliente = await _clienteRepository.ObterPorIdAsync(notification.ClienteId, cancellationToken);
        if (string.IsNullOrWhiteSpace(cliente?.Email))
        {
            _logger.LogInformation("Cliente {ClienteId} não possui e-mail cadastrado. Link de pagamento da OS {Numero} não enviado.", notification.ClienteId, notification.Numero);
            return;
        }

        var prazo = notification.ExpiraEm.ToOffset(OffsetDeBrasilia)
            .ToString("dd/MM/yyyy 'às' HH:mm", CultureInfo.InvariantCulture);

        var assunto = $"Ordem de serviço {notification.Numero} - link de pagamento";
        var corpo = $"Olá, {cliente.Nome}!\n\n" +
                    $"O orçamento da sua ordem de serviço {notification.Numero} foi aprovado. Para iniciarmos o serviço, faça o pagamento pelo link abaixo:\n\n" +
                    $"{notification.LinkPagamento}\n\n" +
                    $"O pagamento pode ser feito até {prazo} (horário de Brasília). Depois desse prazo a ordem de serviço será cancelada.\n\n" +
                    "Atenciosamente,\nOficina Mecânica";

        await _emailService.EnviarAsync(cliente.Email, assunto, corpo, cancellationToken);
    }
}
