namespace OficinaMecanica.Application.DTOs;

public record VeiculoDto(
    Guid Id,
    string Placa,
    string Marca,
    string Modelo,
    int Ano,
    Guid ClienteId
);
