using Bogus;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Tests.Common.Builders;

public class VeiculoBuilder
{
    private static readonly Faker Faker = new("pt_BR");
    private static readonly string[] Marcas = ["Toyota", "Honda", "Chevrolet", "Volkswagen", "Ford"];
    private static readonly string[] Modelos = ["Corolla", "Civic", "Onix", "Golf", "Fiesta"];

    private string _placa = "ABC1234";
    private string _marca = Faker.PickRandom(Marcas);
    private string _modelo = Faker.PickRandom(Modelos);
    private int _ano = Faker.Random.Int(2000, 2024);
    private Guid _clienteId = Guid.NewGuid();

    public VeiculoBuilder ComPlaca(string placa) { _placa = placa; return this; }
    public VeiculoBuilder ComMarca(string marca) { _marca = marca; return this; }
    public VeiculoBuilder ComModelo(string modelo) { _modelo = modelo; return this; }
    public VeiculoBuilder ComAno(int ano) { _ano = ano; return this; }
    public VeiculoBuilder DoCliente(Guid clienteId) { _clienteId = clienteId; return this; }

    public Veiculo Build() => new(Placa.Criar(_placa), _marca, _modelo, _ano, _clienteId);
}
