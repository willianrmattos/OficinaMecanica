using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Queries.ObterCliente;

public record ObterClientePorDocumentoQuery(string Numero) : IRequest<ClienteDto?>;
