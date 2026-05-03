using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ListarClientes;

public class ListarClientesQueryHandler : IRequestHandler<ListarClientesQuery, PaginacaoResultDto<ClienteDto>>
{
    private readonly IClienteRepository _clienteRepository;

    public ListarClientesQueryHandler(IClienteRepository clienteRepository)
    {
        _clienteRepository = clienteRepository;
    }

    public async Task<PaginacaoResultDto<ClienteDto>> Handle(ListarClientesQuery request, CancellationToken cancellationToken)
    {
        var clientes = await _clienteRepository.ListarAsync(request.Pagina, request.TamanhoPagina, request.FiltroNome, cancellationToken);
        var total = await _clienteRepository.ContarAsync(request.FiltroNome, cancellationToken);

        var dtos = clientes.Select(c => new ClienteDto(
            c.Id, c.Nome, c.Documento.Formatado, c.Documento.Tipo.ToString(),
            c.Email, c.Telefone, c.DataCadastro,
            c.Veiculos.Select(v => new VeiculoDto(v.Id, v.Placa.Valor, v.Marca, v.Modelo, v.Ano, v.ClienteId))
        ));

        return new PaginacaoResultDto<ClienteDto>(
            dtos, total, request.Pagina, request.TamanhoPagina,
            (int)Math.Ceiling(total / (double)request.TamanhoPagina)
        );
    }
}
