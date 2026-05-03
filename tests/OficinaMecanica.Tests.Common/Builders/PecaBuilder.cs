using Bogus;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Tests.Common.Builders;

public class PecaBuilder
{
    private static readonly Faker Faker = new("pt_BR");

    private string _nome = Faker.Commerce.ProductName();
    private string? _descricao = null;
    private string? _codigo = Faker.Random.AlphaNumeric(6).ToUpper();
    private decimal _preco = Math.Round(Faker.Random.Decimal(10m, 500m), 2);
    private int _estoque = Faker.Random.Int(5, 100);
    private int _estoqueMinimo = 2;

    public PecaBuilder ComNome(string nome) { _nome = nome; return this; }
    public PecaBuilder ComDescricao(string? descricao) { _descricao = descricao; return this; }
    public PecaBuilder ComCodigo(string? codigo) { _codigo = codigo; return this; }
    public PecaBuilder ComPreco(decimal preco) { _preco = preco; return this; }
    public PecaBuilder ComEstoque(int estoque) { _estoque = estoque; return this; }
    public PecaBuilder ComEstoqueMinimo(int minimo) { _estoqueMinimo = minimo; return this; }

    public Peca Build() => new(_nome, _descricao, _codigo, _preco, _estoque, _estoqueMinimo);
}
