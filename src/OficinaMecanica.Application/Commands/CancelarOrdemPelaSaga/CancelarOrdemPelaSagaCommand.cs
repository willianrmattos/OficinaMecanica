using MediatR;

namespace OficinaMecanica.Application.Commands.CancelarOrdemPelaSaga;

// Compensacao: o motivo vem de outros servicos (texto livre), por isso e opcional e nunca e rejeitado.
// O comando HTTP de cancelamento (CancelarOrdemDeServicoCommand) e quem exige e limita o motivo.
public record CancelarOrdemPelaSagaCommand(Guid OrdemDeServicoId, string? Motivo) : IRequest<Unit>;
