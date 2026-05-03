using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ObterOrdemDeServico;

public class ObterOrdemDeServicoPorIdQueryHandler : IRequestHandler<ObterOrdemDeServicoPorIdQuery, OrdemDeServicoDto?>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IVeiculoRepository _veiculoRepository;

    public ObterOrdemDeServicoPorIdQueryHandler(
        IOrdemDeServicoRepository ordemRepository,
        IClienteRepository clienteRepository,
        IVeiculoRepository veiculoRepository)
    {
        _ordemRepository = ordemRepository;
        _clienteRepository = clienteRepository;
        _veiculoRepository = veiculoRepository;
    }

    public async Task<OrdemDeServicoDto?> Handle(ObterOrdemDeServicoPorIdQuery request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterPorIdAsync(request.Id, cancellationToken);
        if (ordem == null) return null;

        var cliente = await _clienteRepository.ObterPorIdAsync(ordem.ClienteId, cancellationToken);
        var veiculo = await _veiculoRepository.ObterPorIdAsync(ordem.VeiculoId, cancellationToken);

        return OrdemDeServicoMapper.ToDto(ordem, cliente?.Nome, veiculo?.Placa.Valor);
    }
}

public class ObterOrdemDeServicoPorNumeroQueryHandler : IRequestHandler<ObterOrdemDeServicoPorNumeroQuery, OrdemDeServicoDto?>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IVeiculoRepository _veiculoRepository;

    public ObterOrdemDeServicoPorNumeroQueryHandler(
        IOrdemDeServicoRepository ordemRepository,
        IClienteRepository clienteRepository,
        IVeiculoRepository veiculoRepository)
    {
        _ordemRepository = ordemRepository;
        _clienteRepository = clienteRepository;
        _veiculoRepository = veiculoRepository;
    }

    public async Task<OrdemDeServicoDto?> Handle(ObterOrdemDeServicoPorNumeroQuery request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterPorNumeroAsync(request.Numero, cancellationToken);
        if (ordem == null) return null;

        var cliente = await _clienteRepository.ObterPorIdAsync(ordem.ClienteId, cancellationToken);
        var veiculo = await _veiculoRepository.ObterPorIdAsync(ordem.VeiculoId, cancellationToken);

        return OrdemDeServicoMapper.ToDto(ordem, cliente?.Nome, veiculo?.Placa.Valor);
    }
}
