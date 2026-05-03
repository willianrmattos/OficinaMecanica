using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ObterVeiculo;

public class ObterVeiculoPorIdQueryHandler : IRequestHandler<ObterVeiculoPorIdQuery, VeiculoDto?>
{
    private readonly IVeiculoRepository _veiculoRepository;

    public ObterVeiculoPorIdQueryHandler(IVeiculoRepository veiculoRepository)
    {
        _veiculoRepository = veiculoRepository;
    }

    public async Task<VeiculoDto?> Handle(ObterVeiculoPorIdQuery request, CancellationToken cancellationToken)
    {
        var veiculo = await _veiculoRepository.ObterPorIdAsync(request.Id, cancellationToken);
        if (veiculo is null) return null;

        return new VeiculoDto(veiculo.Id, veiculo.Placa.Valor, veiculo.Marca, veiculo.Modelo, veiculo.Ano, veiculo.ClienteId);
    }
}
