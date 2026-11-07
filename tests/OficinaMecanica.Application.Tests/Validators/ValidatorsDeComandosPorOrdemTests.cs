using FluentAssertions;
using FluentValidation;
using OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento;
using OficinaMecanica.Application.Commands.AprovarOrcamento;
using OficinaMecanica.Application.Commands.FinalizarOrdem;
using OficinaMecanica.Application.Commands.IniciarExecucaoDaOrdem;
using OficinaMecanica.Application.Commands.LiberarEstoque;
using OficinaMecanica.Application.Commands.MarcarDiagnosticoIniciado;
using OficinaMecanica.Application.Commands.RegistrarOrcamentoGerado;
using OficinaMecanica.Application.Commands.ReservarEstoque;

namespace OficinaMecanica.Application.Tests.Validators;

// Estes comandos so carregam o id da OS: a unica regra e ele nao ser vazio.
public class ValidatorsDeComandosPorOrdemTests
{
    public static TheoryData<IValidator, Func<Guid, object>> Comandos => new()
    {
        { new AprovarOrcamentoCommandValidator(), id => new AprovarOrcamentoCommand(id) },
        { new MarcarDiagnosticoIniciadoCommandValidator(), id => new MarcarDiagnosticoIniciadoCommand(id) },
        { new RegistrarOrcamentoGeradoCommandValidator(), id => new RegistrarOrcamentoGeradoCommand(id) },
        { new ReservarEstoqueCommandValidator(), id => new ReservarEstoqueCommand(id) },
        { new LiberarEstoqueCommandValidator(), id => new LiberarEstoqueCommand(id) },
        { new AplicarAprovacaoDoOrcamentoCommandValidator(), id => new AplicarAprovacaoDoOrcamentoCommand(id) },
        { new IniciarExecucaoDaOrdemCommandValidator(), id => new IniciarExecucaoDaOrdemCommand(id) },
        { new FinalizarOrdemCommandValidator(), id => new FinalizarOrdemCommand(id) },
    };

    [Theory]
    [MemberData(nameof(Comandos))]
    public async Task Validate_ComOrdemInformada_DeveSerValido(IValidator validador, Func<Guid, object> criarComando)
    {
        var result = await validador.ValidateAsync(new ValidationContext<object>(criarComando(Guid.NewGuid())));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Comandos))]
    public async Task Validate_SemOrdem_DeveSerInvalido(IValidator validador, Func<Guid, object> criarComando)
    {
        var result = await validador.ValidateAsync(new ValidationContext<object>(criarComando(Guid.Empty)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "OrdemDeServicoId")
            .Which.ErrorMessage.Should().Be("A ordem de serviço é obrigatória.");
    }
}
