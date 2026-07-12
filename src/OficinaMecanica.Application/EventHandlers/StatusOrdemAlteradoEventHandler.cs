using MediatR;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace OficinaMecanica.Application.EventHandlers;

public class StatusOrdemAlteradoEventHandler : INotificationHandler<StatusOrdemAlteradoEvent>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<StatusOrdemAlteradoEventHandler> _logger;

    public StatusOrdemAlteradoEventHandler(
        IOrdemDeServicoRepository ordemRepository,
        IClienteRepository clienteRepository,
        IEmailService emailService,
        ILogger<StatusOrdemAlteradoEventHandler> logger)
    {
        _ordemRepository = ordemRepository;
        _clienteRepository = clienteRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(StatusOrdemAlteradoEvent notification, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterPorIdAsync(notification.OrdemDeServicoId, cancellationToken);
        if (ordem is null)
            return;

        var cliente = await _clienteRepository.ObterPorIdAsync(ordem.ClienteId, cancellationToken);
        if (string.IsNullOrWhiteSpace(cliente?.Email))
        {
            _logger.LogInformation("Cliente {ClienteId} não possui e-mail cadastrado. Notificação da OS {Numero} não enviada.", ordem.ClienteId, notification.Numero);
            return;
        }

        var assunto = $"Ordem de serviço {notification.Numero} - status atualizado";
        var corpo = $"Olá, {cliente.Nome}!\n\n" +
                    $"A sua ordem de serviço {notification.Numero} teve o status atualizado para: {DescreverStatus(notification.NovoStatus)}.\n\n" +
                    "Atenciosamente,\nOficina Mecânica";

        await _emailService.EnviarAsync(cliente.Email, assunto, corpo, cancellationToken);
    }

    private static string DescreverStatus(StatusOrdemDeServico status) => status switch
    {
        StatusOrdemDeServico.OrcamentoRecusado => "Orçamento recusado",
        StatusOrdemDeServico.Recebida => "Recebida",
        StatusOrdemDeServico.EmDiagnostico => "Em diagnóstico",
        StatusOrdemDeServico.AguardandoAprovacao => "Aguardando aprovação do orçamento",
        StatusOrdemDeServico.EmExecucao => "Em execução",
        StatusOrdemDeServico.Finalizada => "Finalizada",
        StatusOrdemDeServico.Entregue => "Entregue",
        _ => status.ToString()
    };
}
