using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.Entities;

public class Peca : AggregateRoot
{
    public string Nome { get; private set; }
    public string? Descricao { get; private set; }
    public string? CodigoReferencia { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public int QuantidadeEstoque { get; private set; }
    public int EstoqueMinimo { get; private set; }
    public bool Ativo { get; private set; }

    private Peca() { } // EF Core

    public Peca(string nome, string? descricao, string? codigoReferencia, decimal precoUnitario, int quantidadeEstoque, int estoqueMinimo)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome da peça é obrigatório.");
        if (precoUnitario <= 0)
            throw new DomainException("O preço unitário deve ser maior que zero.");
        if (quantidadeEstoque < 0)
            throw new DomainException("A quantidade em estoque não pode ser negativa.");
        if (estoqueMinimo < 0)
            throw new DomainException("O estoque mínimo não pode ser negativo.");

        Nome = nome;
        Descricao = descricao;
        CodigoReferencia = codigoReferencia;
        PrecoUnitario = precoUnitario;
        QuantidadeEstoque = quantidadeEstoque;
        EstoqueMinimo = estoqueMinimo;
        Ativo = true;
    }

    public void Atualizar(string nome, string? descricao, string? codigoReferencia, decimal precoUnitario, int estoqueMinimo)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome da peça é obrigatório.");
        if (precoUnitario <= 0)
            throw new DomainException("O preço unitário deve ser maior que zero.");
        if (estoqueMinimo < 0)
            throw new DomainException("O estoque mínimo não pode ser negativo.");

        Nome = nome;
        Descricao = descricao;
        CodigoReferencia = codigoReferencia;
        PrecoUnitario = precoUnitario;
        EstoqueMinimo = estoqueMinimo;
    }

    public void AdicionarEstoque(int quantidade)
    {
        if (quantidade <= 0)
            throw new DomainException("A quantidade a adicionar deve ser maior que zero.");
        QuantidadeEstoque += quantidade;
    }

    public void RemoverEstoque(int quantidade)
    {
        if (quantidade <= 0)
            throw new DomainException("A quantidade a remover deve ser maior que zero.");
        if (quantidade > QuantidadeEstoque)
            throw new DomainException($"Estoque insuficiente para a peça '{Nome}'. Disponível: {QuantidadeEstoque}, Solicitado: {quantidade}.");
        QuantidadeEstoque -= quantidade;
    }

    public bool EstoqueAbaixoDoMinimo => QuantidadeEstoque < EstoqueMinimo;

    public void Desativar() => Ativo = false;
    public void Ativar() => Ativo = true;
}
