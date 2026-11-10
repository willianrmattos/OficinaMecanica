using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Commands.CancelarOrdemDeServico;

public record CancelarOrdemDeServicoCommand(Guid OrdemDeServicoId, string Motivo) : IRequest<SolicitacaoAceitaDto>;
