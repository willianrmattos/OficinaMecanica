using OficinaMecanica.Contracts.Common;

namespace OficinaMecanica.Contracts.Events;

public sealed record OrdemDeServicoAberta(
    Guid MessageId,
    Guid OrdemDeServicoId,
    DateTimeOffset OcorridoEm,
    string Numero,
    Guid ClienteId,
    Guid VeiculoId,
    string Placa,
    string Marca,
    string Modelo,
    string? Observacoes,
    decimal ValorTotal,
    IReadOnlyList<ItemDeServicoMensagem> Servicos,
    IReadOnlyList<ItemDePecaMensagem> Pecas);
