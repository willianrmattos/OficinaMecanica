using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ObterCliente;

public class ObterClienteQueryHandler : IRequestHandler<ObterClientePorIdQuery, ClienteDto?>
{
    private readonly IClienteRepository _clienteRepository;

    public ObterClienteQueryHandler(IClienteRepository clienteRepository)
    {
        _clienteRepository = clienteRepository;
    }

    public async Task<ClienteDto?> Handle(ObterClientePorIdQuery request, CancellationToken cancellationToken)
    {
        var cliente = await _clienteRepository.ObterPorIdAsync(request.Id, cancellationToken);
        if (cliente == null) return null;

        return new ClienteDto(
            cliente.Id, cliente.Nome, cliente.Documento.Formatado, cliente.Documento.Tipo.ToString(),
            cliente.Email, cliente.Telefone, cliente.DataCadastro,
            cliente.Veiculos.Select(v => new VeiculoDto(v.Id, v.Placa.Valor, v.Marca, v.Modelo, v.Ano, v.ClienteId))
        );
    }
}
