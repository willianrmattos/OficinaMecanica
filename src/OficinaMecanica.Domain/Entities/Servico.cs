using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.Entities;

public class Servico : AggregateRoot
{
    public string Nome { get; private set; }
    public string? Descricao { get; private set; }
    public decimal Preco { get; private set; }
    public int TempoEstimadoMinutos { get; private set; }
    public bool Ativo { get; private set; }

    private Servico() { } // EF Core

    public Servico(string nome, string? descricao, decimal preco, int tempoEstimadoMinutos)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do serviço é obrigatório.");
        if (preco <= 0)
            throw new DomainException("O preço do serviço deve ser maior que zero.");
        if (tempoEstimadoMinutos <= 0)
            throw new DomainException("O tempo estimado deve ser maior que zero.");

        Nome = nome;
        Descricao = descricao;
        Preco = preco;
        TempoEstimadoMinutos = tempoEstimadoMinutos;
        Ativo = true;
    }

    public void Atualizar(string nome, string? descricao, decimal preco, int tempoEstimadoMinutos)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do serviço é obrigatório.");
        if (preco <= 0)
            throw new DomainException("O preço do serviço deve ser maior que zero.");
        if (tempoEstimadoMinutos <= 0)
            throw new DomainException("O tempo estimado deve ser maior que zero.");

        Nome = nome;
        Descricao = descricao;
        Preco = preco;
        TempoEstimadoMinutos = tempoEstimadoMinutos;
    }

    public void Desativar() => Ativo = false;
    public void Ativar() => Ativo = true;
}
