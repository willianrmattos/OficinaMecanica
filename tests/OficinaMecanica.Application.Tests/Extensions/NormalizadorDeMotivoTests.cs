using FluentAssertions;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Application.Tests.Extensions;

public class NormalizadorDeMotivoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Truncar_SemTexto_DeveDevolverNulo(string? motivo)
    {
        NormalizadorDeMotivo.Truncar(motivo).Should().BeNull();
    }

    [Fact]
    public void Truncar_TextoComEspacosNasPontas_DeveApar()
    {
        NormalizadorDeMotivo.Truncar("  Pagamento expirado.  ").Should().Be("Pagamento expirado.");
    }

    [Fact]
    public void Truncar_TextoNoLimiteExato_DeveManter()
    {
        var texto = new string('a', OrdemDeServico.TamanhoMaximoDoMotivo);

        NormalizadorDeMotivo.Truncar(texto).Should().Be(texto);
    }

    [Fact]
    public void Truncar_TextoMaiorQueOLimite_DeveCortarNoLimite()
    {
        var texto = new string('a', OrdemDeServico.TamanhoMaximoDoMotivo) + "cauda";

        NormalizadorDeMotivo.Truncar(texto).Should().Be(new string('a', OrdemDeServico.TamanhoMaximoDoMotivo));
    }

    [Fact]
    public void Truncar_CorteNoMeioDeUmEmoji_NaoDeveGravarMeioCaractere()
    {
        var texto = new string('a', OrdemDeServico.TamanhoMaximoDoMotivo - 1) + "🔧 depois";

        NormalizadorDeMotivo.Truncar(texto).Should().Be(new string('a', OrdemDeServico.TamanhoMaximoDoMotivo - 1));
    }

    [Fact]
    public void Normalizar_SemTexto_DeveDevolverOMotivoPadrao()
    {
        NormalizadorDeMotivo.Normalizar(null).Should().Be(NormalizadorDeMotivo.MotivoPadrao);
        NormalizadorDeMotivo.Normalizar("  ").Should().Be(NormalizadorDeMotivo.MotivoPadrao);
    }

    [Fact]
    public void Normalizar_ComTexto_DeveDevolverOTextoAparado()
    {
        NormalizadorDeMotivo.Normalizar(" Cliente desistiu. ").Should().Be("Cliente desistiu.");
    }
}
