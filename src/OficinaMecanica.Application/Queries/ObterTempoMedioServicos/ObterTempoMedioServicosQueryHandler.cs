using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ObterTempoMedioServicos;

public class ObterTempoMedioServicosQueryHandler : IRequestHandler<ObterTempoMedioServicosQuery, TempoMedioServicoDto>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;

    public ObterTempoMedioServicosQueryHandler(IOrdemDeServicoRepository ordemRepository)
    {
        _ordemRepository = ordemRepository;
    }

    public async Task<TempoMedioServicoDto> Handle(ObterTempoMedioServicosQuery request, CancellationToken cancellationToken)
    {
        var tempoMedio = await _ordemRepository.ObterTempoMedioExecucaoAsync(cancellationToken);
        return new TempoMedioServicoDto(tempoMedio, $"Tempo médio de execução: {tempoMedio:F1} horas");
    }
}
