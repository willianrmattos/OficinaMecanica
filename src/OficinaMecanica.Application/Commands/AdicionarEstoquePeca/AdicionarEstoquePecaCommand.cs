using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Commands.AdicionarEstoquePeca;

public record AdicionarEstoquePecaCommand(Guid Id, int Quantidade) : IRequest<PecaDto>;
