using Bogus;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Tests.Common.Builders;

public class OrdemDeServicoAbertaBuilder
{
    private static readonly Faker Faker = new("pt_BR");

    private Guid _messageId = Guid.NewGuid();
    private Guid _ordemDeServicoId = Guid.NewGuid();
    private Guid _clienteId = Guid.NewGuid();
    private Guid _veiculoId = Guid.NewGuid();
    private string _numero = $"OS-{Faker.Random.Int(100000, 999999)}";
    private string _placa = "ABC1D23";
    private string _marca = Faker.PickRandom("Toyota", "Honda", "Chevrolet");
    private string _modelo = Faker.PickRandom("Corolla", "Civic", "Onix");
    private readonly List<ItemDeServicoMensagem> _servicos = new();

    public OrdemDeServicoAbertaBuilder ComMessageId(Guid messageId) { _messageId = messageId; return this; }
    public OrdemDeServicoAbertaBuilder DaOrdem(Guid ordemDeServicoId) { _ordemDeServicoId = ordemDeServicoId; return this; }
    public OrdemDeServicoAbertaBuilder DoCliente(Guid clienteId) { _clienteId = clienteId; return this; }
    public OrdemDeServicoAbertaBuilder DoVeiculo(Guid veiculoId) { _veiculoId = veiculoId; return this; }
    public OrdemDeServicoAbertaBuilder ComPlaca(string placa) { _placa = placa; return this; }

    public OrdemDeServicoAbertaBuilder ComServico(Guid servicoId, string nome, decimal precoUnitario, int quantidade = 1)
    {
        _servicos.Add(new ItemDeServicoMensagem(servicoId, nome, precoUnitario, quantidade));
        return this;
    }

    public OrdemDeServicoAberta Build() =>
        new(_messageId, _ordemDeServicoId, DateTimeOffset.UtcNow, _numero, _clienteId, _veiculoId, _placa, _marca, _modelo,
            null, _servicos.Sum(s => s.PrecoUnitario * s.Quantidade), _servicos.ToList(), new List<ItemDePecaMensagem>());
}
