using MediatR;

namespace OficinaMecanica.Application.Commands.AtualizarVeiculo;

public record AtualizarVeiculoCommand(
    Guid Id,
    string Marca,
    string Modelo,
    int Ano
) : IRequest;
