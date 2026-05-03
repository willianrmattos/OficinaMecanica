using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Application.DTOs;

public static class OrdemDeServicoMapper
{
    public static OrdemDeServicoDto ToDto(OrdemDeServico ordem, string? nomeCliente, string? placaVeiculo) =>
        new(
            ordem.Id, ordem.Numero, ordem.ClienteId, nomeCliente, ordem.VeiculoId, placaVeiculo,
            ordem.Status.ToString(), ordem.ValorTotal, ordem.Observacoes, ordem.DataAbertura, ordem.DataConclusao,
            ordem.ItensServico.Select(i => new ItemServicoDto(i.Id, i.ServicoId, i.NomeServico, i.PrecoUnitario, i.Quantidade, i.Subtotal)),
            ordem.ItensPeca.Select(i => new ItemPecaDto(i.Id, i.PecaId, i.NomePeca, i.PrecoUnitario, i.Quantidade, i.Subtotal)),
            ordem.HistoricoStatus.Select(h => new HistoricoStatusDto(h.Id, h.Status.ToString(), h.Observacao, h.DataAlteracao))
        );
}
