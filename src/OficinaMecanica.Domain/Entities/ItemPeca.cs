using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.Entities;

public class ItemPeca : Entity
{
    public Guid OrdemDeServicoId { get; private set; }
    public Guid PecaId { get; private set; }
    public string NomePeca { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public int Quantidade { get; private set; }
    public decimal Subtotal => PrecoUnitario * Quantidade;

    private ItemPeca() { } // EF Core

    public ItemPeca(Guid ordemDeServicoId, Guid pecaId, string nomePeca, decimal precoUnitario, int quantidade)
    {
        OrdemDeServicoId = ordemDeServicoId;
        PecaId = pecaId;
        NomePeca = nomePeca;
        PrecoUnitario = precoUnitario;
        Quantidade = quantidade;
    }

    public void AtualizarQuantidade(int novaQuantidade)
    {
        if (novaQuantidade <= 0)
            throw new DomainException("A quantidade deve ser maior que zero.");
        Quantidade = novaQuantidade;
    }
}
