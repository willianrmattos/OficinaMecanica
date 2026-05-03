using FluentAssertions;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.Tests.Entities;

public class PecaTests
{
    [Fact]
    public void Construtor_ComDadosValidos_DeveCriarPeca()
    {
        var peca = new PecaBuilder().ComNome("Filtro de óleo").ComEstoque(100).Build();

        peca.Nome.Should().Be("Filtro de óleo");
        peca.QuantidadeEstoque.Should().Be(100);
        peca.Ativo.Should().BeTrue();
    }

    [Fact]
    public void AdicionarEstoque_DeveIncrementar()
    {
        var peca = new PecaBuilder().ComEstoque(10).Build();

        peca.AdicionarEstoque(5);

        peca.QuantidadeEstoque.Should().Be(15);
    }

    [Fact]
    public void RemoverEstoque_ComEstoqueSuficiente_DeveDecrementar()
    {
        var peca = new PecaBuilder().ComEstoque(10).Build();

        peca.RemoverEstoque(3);

        peca.QuantidadeEstoque.Should().Be(7);
    }

    [Fact]
    public void RemoverEstoque_SemEstoqueSuficiente_DeveLancarExcecao()
    {
        var peca = new PecaBuilder().ComEstoque(2).Build();

        var act = () => peca.RemoverEstoque(5);

        act.Should().Throw<DomainException>().WithMessage("*Estoque insuficiente*");
    }

    [Fact]
    public void EstoqueAbaixoDoMinimo_DeveRetornarTrue()
    {
        var peca = new PecaBuilder().ComEstoque(3).ComEstoqueMinimo(5).Build();

        peca.EstoqueAbaixoDoMinimo.Should().BeTrue();
    }

    [Fact]
    public void Desativar_DeveMarcarComoInativo()
    {
        var peca = new PecaBuilder().Build();

        peca.Desativar();

        peca.Ativo.Should().BeFalse();
    }
}
