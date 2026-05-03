using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Domain.Entities;

public class Veiculo : Entity
{
    public Placa Placa { get; private set; }
    public string Marca { get; private set; }
    public string Modelo { get; private set; }
    public int Ano { get; private set; }
    public Guid ClienteId { get; private set; }

    private Veiculo() { } // EF Core

    public Veiculo(Placa placa, string marca, string modelo, int ano, Guid clienteId)
    {
        if (string.IsNullOrWhiteSpace(marca))
            throw new DomainException("A marca do veículo é obrigatória.");
        if (string.IsNullOrWhiteSpace(modelo))
            throw new DomainException("O modelo do veículo é obrigatório.");
        if (ano < 1900 || ano > DateTime.Now.Year + 1)
            throw new DomainException($"O ano do veículo deve estar entre 1900 e {DateTime.Now.Year + 1}.");

        Placa = placa ?? throw new DomainException("A placa do veículo é obrigatória.");
        Marca = marca;
        Modelo = modelo;
        Ano = ano;
        ClienteId = clienteId;
    }

    public void Atualizar(string marca, string modelo, int ano)
    {
        if (string.IsNullOrWhiteSpace(marca))
            throw new DomainException("A marca do veículo é obrigatória.");
        if (string.IsNullOrWhiteSpace(modelo))
            throw new DomainException("O modelo do veículo é obrigatório.");
        if (ano < 1900 || ano > DateTime.Now.Year + 1)
            throw new DomainException($"O ano do veículo deve estar entre 1900 e {DateTime.Now.Year + 1}.");

        Marca = marca;
        Modelo = modelo;
        Ano = ano;
    }
}
