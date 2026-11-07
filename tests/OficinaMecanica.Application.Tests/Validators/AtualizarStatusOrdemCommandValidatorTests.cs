using FluentAssertions;
using OficinaMecanica.Application.Commands.AtualizarStatusOrdem;
using OficinaMecanica.Domain.Enums;

namespace OficinaMecanica.Application.Tests.Validators;

public class AtualizarStatusOrdemCommandValidatorTests
{
    private readonly AtualizarStatusOrdemCommandValidator _validator = new();

    [Fact]
    public async Task Validate_StatusEntregue_DeveSerValido()
    {
        var command = new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)StatusOrdemDeServico.Entregue);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.OrcamentoRecusado)]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.EmExecucao)]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public async Task Validate_QualquerStatusQueNaoSejaEntregue_DeveSerInvalido(StatusOrdemDeServico status)
    {
        var command = new AtualizarStatusOrdemCommand(Guid.NewGuid(), (int)status);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "NovoStatus")
            .Which.ErrorMessage.Should().Contain("'Entregue'");
    }

    [Theory]
    [InlineData(999)]
    [InlineData(-1)]
    public async Task Validate_StatusInexistente_DeveSerInvalido(int status)
    {
        var command = new AtualizarStatusOrdemCommand(Guid.NewGuid(), status);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NovoStatus");
    }

    [Fact]
    public async Task Validate_SemOrdemDeServico_DeveSerInvalido()
    {
        var command = new AtualizarStatusOrdemCommand(Guid.Empty, (int)StatusOrdemDeServico.Entregue);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrdemDeServicoId");
    }
}
