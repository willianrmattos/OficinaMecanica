using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Commands.CriarVeiculo;

public record CriarVeiculoCommand(
    Guid ClienteId,
    string Placa,
    string Marca,
    string Modelo,
    int Ano
) : IRequest<VeiculoDto>;
