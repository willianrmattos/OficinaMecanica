using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Application.Queries.ObterCliente;

public class ObterClientePorDocumentoQueryHandler : IRequestHandler<ObterClientePorDocumentoQuery, ClienteDto?>
{
    private readonly IClienteRepository _clienteRepository;

    public ObterClientePorDocumentoQueryHandler(IClienteRepository clienteRepository)
    {
        _clienteRepository = clienteRepository;
    }

    public async Task<ClienteDto?> Handle(ObterClientePorDocumentoQuery request, CancellationToken cancellationToken)
    {
        Documento documento;
        try
        {
            documento = Documento.Criar(request.Numero);
        }
        catch (DomainException)
        {
            return null;
        }

        var cliente = await _clienteRepository.ObterPorDocumentoAsync(documento, cancellationToken);
        if (cliente == null) return null;

        return new ClienteDto(
            cliente.Id, cliente.Nome, cliente.Documento.Formatado, cliente.Documento.Tipo.ToString(),
            cliente.Email, cliente.Telefone, cliente.DataCadastro,
            cliente.Veiculos.Select(v => new VeiculoDto(v.Id, v.Placa.Valor, v.Marca, v.Modelo, v.Ano, v.ClienteId))
        );
    }
}
