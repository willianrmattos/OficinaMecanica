using FluentAssertions;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Domain.Tests.ValueObjects;

public class DocumentoTests
{
    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    public void Criar_ComCpfValido_DeveCriarDocumento(string cpf)
    {
        var documento = Documento.Criar(cpf);
        documento.Numero.Should().Be("52998224725");
        documento.Tipo.Should().Be(Enums.TipoDocumento.CPF);
    }

    [Theory]
    [InlineData("11222333000181")]
    [InlineData("11.222.333/0001-81")]
    public void Criar_ComCnpjValido_DeveCriarDocumento(string cnpj)
    {
        var documento = Documento.Criar(cnpj);
        documento.Numero.Should().Be("11222333000181");
        documento.Tipo.Should().Be(Enums.TipoDocumento.CNPJ);
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("12345678900")]
    [InlineData("123")]
    [InlineData("")]
    public void Criar_ComDocumentoInvalido_DeveLancarExcecao(string documento)
    {
        var act = () => Documento.Criar(documento);
        act.Should().Throw<DomainException>().WithMessage("*inválido*");
    }

    [Fact]
    public void Formatado_Cpf_DeveRetornarFormatado()
    {
        var doc = Documento.Criar("52998224725");
        doc.Formatado.Should().Be("529.982.247-25");
    }

    [Fact]
    public void Formatado_Cnpj_DeveRetornarFormatado()
    {
        var doc = Documento.Criar("11222333000181");
        doc.Formatado.Should().Be("11.222.333/0001-81");
    }

    [Fact]
    public void Equals_MesmoNumero_DeveSerIgual()
    {
        var doc1 = Documento.Criar("52998224725");
        var doc2 = Documento.Criar("529.982.247-25");
        doc1.Should().Be(doc2);
    }
}
