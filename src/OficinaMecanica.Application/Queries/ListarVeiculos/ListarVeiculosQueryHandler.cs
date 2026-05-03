using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ListarVeiculos;

public class ListarVeiculosQueryHandler : IRequestHandler<ListarVeiculosPorClienteQuery, IEnumerable<VeiculoDto>>
{
    private readonly IVeiculoRepository _veiculoRepository;

    public ListarVeiculosQueryHandler(IVeiculoRepository veiculoRepository)
    {
        _veiculoRepository = veiculoRepository;
    }

    public async Task<IEnumerable<VeiculoDto>> Handle(ListarVeiculosPorClienteQuery request, CancellationToken cancellationToken)
    {
        var veiculos = await _veiculoRepository.ListarPorClienteAsync(request.ClienteId, cancellationToken);
        return veiculos.Select(v => new VeiculoDto(v.Id, v.Placa.Valor, v.Marca, v.Modelo, v.Ano, v.ClienteId));
    }
}
