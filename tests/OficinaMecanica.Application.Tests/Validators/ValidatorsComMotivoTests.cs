using FluentAssertions;
using FluentValidation;
using OficinaMecanica.Application.Commands.AplicarRecusaDoOrcamento;
using OficinaMecanica.Application.Commands.CancelarOrdemPelaSaga;
using OficinaMecanica.Application.Commands.CancelarOrdemDeServico;
using OficinaMecanica.Application.Commands.RecusarOrcamento;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Application.Tests.Validators;

// O motivo livre acaba no historico da OS: os comandos de entrada (HTTP e decisao do cliente) o limitam em 400
// caracteres. O comando interno de compensacao da saga nao o rejeita nunca, para conseguir cancelar a OS.
public class ValidatorsComMotivoTests
{
    private static readonly string MotivoNoLimite = new('x', OrdemDeServico.TamanhoMaximoDoMotivo);
    private static readonly string MotivoAcimaDoLimite = new('x', OrdemDeServico.TamanhoMaximoDoMotivo + 1);

    private static async Task<FluentValidation.Results.ValidationResult> Validar(IValidator validador, object comando) =>
        await validador.ValidateAsync(new ValidationContext<object>(comando));

    [Fact]
    public async Task CancelarOrdemDeServico_ComMotivo_DeveSerValido()
    {
        var result = await Validar(new CancelarOrdemDeServicoCommandValidator(), new CancelarOrdemDeServicoCommand(Guid.NewGuid(), MotivoNoLimite));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelarOrdemDeServico_SemMotivo_DeveSerInvalido(string motivo)
    {
        var result = await Validar(new CancelarOrdemDeServicoCommandValidator(), new CancelarOrdemDeServicoCommand(Guid.NewGuid(), motivo));

        result.Errors.Should().Contain(e => e.PropertyName == "Motivo" && e.ErrorMessage.Contains("obrigatório"));
    }

    [Fact]
    public async Task CancelarOrdemDeServico_MotivoAcimaDoLimite_DeveSerInvalido()
    {
        var result = await Validar(new CancelarOrdemDeServicoCommandValidator(), new CancelarOrdemDeServicoCommand(Guid.NewGuid(), MotivoAcimaDoLimite));

        result.Errors.Should().Contain(e => e.PropertyName == "Motivo" && e.ErrorMessage.Contains("no máximo"));
    }

    [Fact]
    public async Task CancelarOrdemDeServico_SemOrdem_DeveSerInvalido()
    {
        var result = await Validar(new CancelarOrdemDeServicoCommandValidator(), new CancelarOrdemDeServicoCommand(Guid.Empty, "Motivo."));

        result.Errors.Should().Contain(e => e.PropertyName == "OrdemDeServicoId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Pagamento não realizado no prazo.")]
    public async Task CancelarOrdemPelaSaga_ComQualquerMotivo_DeveSerValidoPoisOHandlerNormaliza(string? motivo)
    {
        var result = await Validar(new CancelarOrdemPelaSagaCommandValidator(), new CancelarOrdemPelaSagaCommand(Guid.NewGuid(), motivo));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CancelarOrdemPelaSaga_MotivoAcimaDoLimite_DeveSerValidoPoisOHandlerTrunca()
    {
        var result = await Validar(new CancelarOrdemPelaSagaCommandValidator(), new CancelarOrdemPelaSagaCommand(Guid.NewGuid(), MotivoAcimaDoLimite));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CancelarOrdemPelaSaga_SemOrdem_DeveSerInvalido()
    {
        var result = await Validar(new CancelarOrdemPelaSagaCommandValidator(), new CancelarOrdemPelaSagaCommand(Guid.Empty, "Motivo."));

        result.Errors.Should().Contain(e => e.PropertyName == "OrdemDeServicoId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Achei caro.")]
    public async Task RecusarOrcamento_ComMotivoOpcional_DeveSerValido(string? motivo)
    {
        var result = await Validar(new RecusarOrcamentoCommandValidator(), new RecusarOrcamentoCommand(Guid.NewGuid(), motivo));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RecusarOrcamento_MotivoAcimaDoLimite_DeveSerInvalido()
    {
        var result = await Validar(new RecusarOrcamentoCommandValidator(), new RecusarOrcamentoCommand(Guid.NewGuid(), MotivoAcimaDoLimite));

        result.Errors.Should().Contain(e => e.PropertyName == "Motivo" && e.ErrorMessage.Contains("no máximo"));
    }

    [Fact]
    public async Task RecusarOrcamento_SemOrdem_DeveSerInvalido()
    {
        var result = await Validar(new RecusarOrcamentoCommandValidator(), new RecusarOrcamentoCommand(Guid.Empty));

        result.Errors.Should().Contain(e => e.PropertyName == "OrdemDeServicoId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Achei caro.")]
    public async Task AplicarRecusaDoOrcamento_ComMotivoOpcional_DeveSerValido(string? motivo)
    {
        var result = await Validar(new AplicarRecusaDoOrcamentoCommandValidator(), new AplicarRecusaDoOrcamentoCommand(Guid.NewGuid(), motivo));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AplicarRecusaDoOrcamento_MotivoAcimaDoLimite_DeveSerInvalido()
    {
        var result = await Validar(new AplicarRecusaDoOrcamentoCommandValidator(), new AplicarRecusaDoOrcamentoCommand(Guid.NewGuid(), MotivoAcimaDoLimite));

        result.Errors.Should().Contain(e => e.PropertyName == "Motivo" && e.ErrorMessage.Contains("no máximo"));
    }

    [Fact]
    public async Task AplicarRecusaDoOrcamento_SemOrdem_DeveSerInvalido()
    {
        var result = await Validar(new AplicarRecusaDoOrcamentoCommandValidator(), new AplicarRecusaDoOrcamentoCommand(Guid.Empty));

        result.Errors.Should().Contain(e => e.PropertyName == "OrdemDeServicoId");
    }
}
