namespace OficinaMecanica.Application.DTOs;

public record ClienteDto(
    Guid Id,
    string Nome,
    string Documento,
    string TipoDocumento,
    string? Email,
    string? Telefone,
    DateTime DataCadastro,
    IEnumerable<VeiculoDto> Veiculos
);
