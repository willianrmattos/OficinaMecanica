using FluentAssertions;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Domain.Tests.Entities;

public class ClienteTests
{
    [Fact]
    public void Construtor_ComDadosValidos_DeveCriarCliente()
    {
        var cliente = new ClienteBuilder()
            .ComNome("João Silva")
            .ComDocumento("52998224725")
            .ComEmail("joao@email.com")
            .Build();

        cliente.Nome.Should().Be("João Silva");
        cliente.Email.Should().Be("joao@email.com");
        cliente.DataCadastro.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Construtor_SemNome_DeveLancarExcecao()
    {
        var doc = Documento.Criar("52998224725");
        var act = () => new Cliente("", doc, null, null);
        act.Should().Throw<DomainException>().WithMessage("*nome*obrigatório*");
    }

    [Fact]
    public void AdicionarVeiculo_VeiculoNovo_DeveAdicionar()
    {
        var cliente = new ClienteBuilder().Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();

        cliente.AdicionarVeiculo(veiculo);

        cliente.Veiculos.Should().HaveCount(1);
    }

    [Fact]
    public void AdicionarVeiculo_PlacaDuplicada_DeveLancarExcecao()
    {
        var cliente = new ClienteBuilder().Build();
        var v1 = new VeiculoBuilder().ComPlaca("ABC1234").DoCliente(cliente.Id).Build();
        var v2 = new VeiculoBuilder().ComPlaca("ABC1234").DoCliente(cliente.Id).Build();

        cliente.AdicionarVeiculo(v1);
        var act = () => cliente.AdicionarVeiculo(v2);

        act.Should().Throw<DomainException>().WithMessage("*já está cadastrado*");
    }

    [Fact]
    public void Atualizar_ComDadosValidos_DeveAtualizar()
    {
        var cliente = new ClienteBuilder().SemEmail().Build();

        cliente.Atualizar("Maria Silva", "maria@email.com", "11888888888");

        cliente.Nome.Should().Be("Maria Silva");
        cliente.Email.Should().Be("maria@email.com");
    }
}
