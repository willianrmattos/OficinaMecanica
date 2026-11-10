using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ObterSnapshotDaOrdem;

// Consulta interna, usada pela saga ao montar GerarOrcamento e IniciarExecucao: nao e exposta por HTTP.
public record ObterSnapshotDaOrdemQuery(Guid OrdemDeServicoId) : IRequest<SnapshotDaOrdemDto>;
