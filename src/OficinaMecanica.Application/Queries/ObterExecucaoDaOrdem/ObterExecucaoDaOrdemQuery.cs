using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ObterExecucaoDaOrdem;

// Consulta detalhada da execucao de uma OS. Devolve nulo quando a OS nao existe (404); indisponibilidade da Execucao NAO e
// erro: o handler cai no ultimo estado conhecido da saga.
public record ObterExecucaoDaOrdemQuery(Guid OrdemDeServicoId) : IRequest<ExecucaoDaOrdemDto?>;
