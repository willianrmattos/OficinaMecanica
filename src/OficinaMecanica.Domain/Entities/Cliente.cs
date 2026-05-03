using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Domain.Entities;

public class Cliente : AggregateRoot
{
    public string Nome { get; private set; }
    public Documento Documento { get; private set; }
    public string? Email { get; private set; }
    public string? Telefone { get; private set; }
    public DateTime DataCadastro { get; private set; }

    private readonly List<Veiculo> _veiculos = new();
    public IReadOnlyCollection<Veiculo> Veiculos => _veiculos.AsReadOnly();

    private Cliente() { } // EF Core

    public Cliente(string nome, Documento documento, string? email, string? telefone)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do cliente é obrigatório.");

        Nome = nome;
        Documento = documento ?? throw new DomainException("O documento do cliente é obrigatório.");
        Email = email;
        Telefone = telefone;
        DataCadastro = DateTime.UtcNow;
    }

    public void Atualizar(string nome, string? email, string? telefone)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do cliente é obrigatório.");

        Nome = nome;
        Email = email;
        Telefone = telefone;
    }

    public void AdicionarVeiculo(Veiculo veiculo)
    {
        if (_veiculos.Any(v => v.Placa == veiculo.Placa))
            throw new DomainException($"O veículo com placa {veiculo.Placa} já está cadastrado para este cliente.");

        _veiculos.Add(veiculo);
    }
}
