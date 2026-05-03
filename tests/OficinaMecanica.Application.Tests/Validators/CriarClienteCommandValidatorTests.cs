using FluentAssertions;
using OficinaMecanica.Application.Commands.CriarCliente;

namespace OficinaMecanica.Application.Tests.Validators;

public class CriarClienteCommandValidatorTests
{
    private readonly CriarClienteCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ComDadosValidos_DeveSerValido()
    {
        var command = new CriarClienteCommand("João Silva", "52998224725", "joao@email.com", "11999999999");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_SemNome_DeveSerInvalido()
    {
        var command = new CriarClienteCommand("", "52998224725", null, null);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Nome");
    }

    [Fact]
    public async Task Validate_SemDocumento_DeveSerInvalido()
    {
        var command = new CriarClienteCommand("João", "", null, null);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Documento");
    }

    [Fact]
    public async Task Validate_ComEmailInvalido_DeveSerInvalido()
    {
        var command = new CriarClienteCommand("João", "52998224725", "invalido", null);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }
}
