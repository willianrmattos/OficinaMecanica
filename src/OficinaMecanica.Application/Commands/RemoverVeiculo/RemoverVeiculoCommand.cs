using MediatR;

namespace OficinaMecanica.Application.Commands.RemoverVeiculo;

public record RemoverVeiculoCommand(Guid Id) : IRequest;
