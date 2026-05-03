using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ListarOrdensDeServico;

public class ListarOrdensDeServicoQueryHandler : IRequestHandler<ListarOrdensDeServicoQuery, PaginacaoResultDto<OrdemDeServicoDto>>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IVeiculoRepository _veiculoRepository;

    public ListarOrdensDeServicoQueryHandler(
        IOrdemDeServicoRepository ordemRepository,
        IClienteRepository clienteRepository,
        IVeiculoRepository veiculoRepository)
    {
        _ordemRepository = ordemRepository;
        _clienteRepository = clienteRepository;
        _veiculoRepository = veiculoRepository;
    }

    public async Task<PaginacaoResultDto<OrdemDeServicoDto>> Handle(ListarOrdensDeServicoQuery request, CancellationToken cancellationToken)
    {
        StatusOrdemDeServico? filtroStatus = null;
        if (!string.IsNullOrEmpty(request.FiltroStatus) && Enum.TryParse<StatusOrdemDeServico>(request.FiltroStatus, true, out var status))
            filtroStatus = status;

        var ordens = await _ordemRepository.ListarAsync(request.Pagina, request.TamanhoPagina, filtroStatus, cancellationToken);
        var total = await _ordemRepository.ContarAsync(filtroStatus, cancellationToken);

        var dtos = new List<OrdemDeServicoDto>();
        foreach (var ordem in ordens)
        {
            var cliente = await _clienteRepository.ObterPorIdAsync(ordem.ClienteId, cancellationToken);
            var veiculo = await _veiculoRepository.ObterPorIdAsync(ordem.VeiculoId, cancellationToken);

            dtos.Add(OrdemDeServicoMapper.ToDto(ordem, cliente?.Nome, veiculo?.Placa.Valor));
        }

        return new PaginacaoResultDto<OrdemDeServicoDto>(
            dtos, total, request.Pagina, request.TamanhoPagina,
            (int)Math.Ceiling(total / (double)request.TamanhoPagina)
        );
    }
}
