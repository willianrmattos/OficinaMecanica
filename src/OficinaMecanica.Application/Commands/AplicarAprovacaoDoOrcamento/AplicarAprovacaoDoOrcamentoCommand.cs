using MediatR;

namespace OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento;

public record AplicarAprovacaoDoOrcamentoCommand(Guid OrdemDeServicoId) : IRequest<Unit>;
