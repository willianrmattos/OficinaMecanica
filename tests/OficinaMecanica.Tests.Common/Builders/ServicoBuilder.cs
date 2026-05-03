using Bogus;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Tests.Common.Builders;

public class ServicoBuilder
{
    private static readonly Faker Faker = new("pt_BR");

    private string _nome = Faker.Commerce.ProductName();
    private string? _descricao = null;
    private decimal _preco = Math.Round(Faker.Random.Decimal(50m, 500m), 2);
    private int _tempo = Faker.Random.Int(15, 180);

    public ServicoBuilder ComNome(string nome) { _nome = nome; return this; }
    public ServicoBuilder ComDescricao(string? descricao) { _descricao = descricao; return this; }
    public ServicoBuilder ComPreco(decimal preco) { _preco = preco; return this; }
    public ServicoBuilder ComTempo(int minutos) { _tempo = minutos; return this; }

    public Servico Build() => new(_nome, _descricao, _preco, _tempo);
}
