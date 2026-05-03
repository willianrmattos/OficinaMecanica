using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.Entities;

public class ItemServico : Entity
{
    public Guid OrdemDeServicoId { get; private set; }
    public Guid ServicoId { get; private set; }
    public string NomeServico { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public int Quantidade { get; private set; }
    public decimal Subtotal => PrecoUnitario * Quantidade;

    private ItemServico() { } // EF Core

    public ItemServico(Guid ordemDeServicoId, Guid servicoId, string nomeServico, decimal precoUnitario, int quantidade)
    {
        OrdemDeServicoId = ordemDeServicoId;
        ServicoId = servicoId;
        NomeServico = nomeServico;
        PrecoUnitario = precoUnitario;
        Quantidade = quantidade;
    }
}
