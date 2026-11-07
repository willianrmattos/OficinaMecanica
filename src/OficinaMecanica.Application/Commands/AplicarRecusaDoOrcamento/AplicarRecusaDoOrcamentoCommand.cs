using MediatR;

namespace OficinaMecanica.Application.Commands.AplicarRecusaDoOrcamento;

public record AplicarRecusaDoOrcamentoCommand(Guid OrdemDeServicoId, string? Motivo = null) : IRequest<Unit>;
