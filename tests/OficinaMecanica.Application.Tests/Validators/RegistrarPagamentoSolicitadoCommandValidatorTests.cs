using FluentAssertions;
using OficinaMecanica.Application.Commands.RegistrarPagamentoSolicitado;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Application.Tests.Validators;

public class RegistrarPagamentoSolicitadoCommandValidatorTests
{
    private static readonly DateTimeOffset Prazo = new(2026, 3, 12, 15, 30, 0, TimeSpan.Zero);

    private readonly RegistrarPagamentoSolicitadoCommandValidator _validator = new();

    private static RegistrarPagamentoSolicitadoCommand Comando(
        Guid? ordemId = null, Guid? pagamentoId = null, string link = "https://pagamento.exemplo/checkout/1", DateTimeOffset? expiraEm = null) =>
        new(ordemId ?? Guid.NewGuid(), pagamentoId ?? Guid.NewGuid(), link, expiraEm ?? Prazo);

    [Theory]
    [InlineData("https://pagamento.exemplo/checkout/1")]
    [InlineData("http://localhost:8081/pagamento/1")]
    public async Task Validate_ComUrlHttpOuHttps_DeveSerValido(string link)
    {
        var result = await _validator.ValidateAsync(Comando(link: link));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_SemLink_DeveSerInvalido(string link)
    {
        var result = await _validator.ValidateAsync(Comando(link: link));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LinkPagamento");
    }

    [Theory]
    [InlineData("pagamento.exemplo/checkout/1")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://pagamento.exemplo/arquivo")]
    [InlineData("/checkout/1")]
    public async Task Validate_ComLinkQueNaoEhUrlHttpAbsoluta_DeveSerInvalido(string link)
    {
        var result = await _validator.ValidateAsync(Comando(link: link));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LinkPagamento" && e.ErrorMessage.Contains("http"));
    }

    [Fact]
    public async Task Validate_LinkMaiorQueOLimite_DeveSerInvalido()
    {
        var link = "https://pagamento.exemplo/" + new string('a', OrdemDeServico.TamanhoMaximoDoLinkDePagamento);

        var result = await _validator.ValidateAsync(Comando(link: link));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LinkPagamento" && e.ErrorMessage.Contains("no máximo"));
    }

    [Fact]
    public async Task Validate_SemOrdemDeServico_DeveSerInvalido()
    {
        var result = await _validator.ValidateAsync(Comando(ordemId: Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrdemDeServicoId");
    }

    [Fact]
    public async Task Validate_SemPagamento_DeveSerInvalido()
    {
        var result = await _validator.ValidateAsync(Comando(pagamentoId: Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PagamentoId");
    }

    [Fact]
    public async Task Validate_SemPrazo_DeveSerInvalido()
    {
        var result = await _validator.ValidateAsync(Comando(expiraEm: default(DateTimeOffset)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExpiraEm");
    }
}
