using FluentAssertions;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Domain.Tests.ValueObjects;

public class PlacaTests
{
    [Theory]
    [InlineData("ABC1234")]
    [InlineData("abc1234")]
    [InlineData("ABC-1234")]
    public void Criar_ComPlacaAntigaValida_DeveCriarPlaca(string placa)
    {
        var resultado = Placa.Criar(placa);
        resultado.Valor.Should().Be("ABC1234");
    }

    [Theory]
    [InlineData("ABC1D23")]
    [InlineData("abc1d23")]
    public void Criar_ComPlacaMercosulValida_DeveCriarPlaca(string placa)
    {
        var resultado = Placa.Criar(placa);
        resultado.Valor.Should().Be("ABC1D23");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC123")]
    [InlineData("ABCD1234")]
    [InlineData("1234567")]
    public void Criar_ComPlacaInvalida_DeveLancarExcecao(string placa)
    {
        var act = () => Placa.Criar(placa);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Equals_MesmaPlaca_DeveSerIgual()
    {
        var p1 = Placa.Criar("ABC1234");
        var p2 = Placa.Criar("abc-1234");
        p1.Should().Be(p2);
    }
}
