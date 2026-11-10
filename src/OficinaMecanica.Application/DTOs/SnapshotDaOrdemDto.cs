using OficinaMecanica.Contracts.Common;

namespace OficinaMecanica.Application.DTOs;

// Retrato da OS no formato das mensagens da saga: o que a saga leva ao Billing (orcamento) e a Execucao
// (reparo aprovado). E so leitura: quem monta e quem consome nunca mexem no agregado.
public record SnapshotDaOrdemDto(
    string Numero,
    decimal ValorTotal,
    IReadOnlyList<ItemDeServicoMensagem> Servicos,
    IReadOnlyList<ItemDePecaMensagem> Pecas);
