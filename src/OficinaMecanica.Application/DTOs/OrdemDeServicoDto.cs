namespace OficinaMecanica.Application.DTOs;

public record OrdemDeServicoDto(
    Guid Id,
    string Numero,
    Guid ClienteId,
    string? NomeCliente,
    Guid VeiculoId,
    string? PlacaVeiculo,
    string Status,
    decimal ValorTotal,
    string? Observacoes,
    DateTime DataAbertura,
    DateTime? DataConclusao,
    IEnumerable<ItemServicoDto> ItensServico,
    IEnumerable<ItemPecaDto> ItensPeca,
    IEnumerable<HistoricoStatusDto> HistoricoStatus
);
