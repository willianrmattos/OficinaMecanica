using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Saga.Activities;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

public class SagaOptionsETiposDaSagaTests
{
    [Fact]
    public void Padroes_DevemSerOsDoPlano()
    {
        var opcoes = new SagaOptions();

        opcoes.PrazoAprovacaoHoras.Should().Be(72);
        opcoes.PrazoPagamentoHoras.Should().Be(48);
        opcoes.SweeperIntervaloSegundos.Should().Be(30);
        opcoes.SweeperLote.Should().Be(50);
        opcoes.SweeperHabilitado.Should().BeTrue();
        SagaOptions.Secao.Should().Be("Saga");
    }

    [Fact]
    public void Validador_ComPadroes_DeveAceitar()
    {
        new ValidadorDasOpcoesDaSaga().Validate(null, new SagaOptions()).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(SagaOptions.PrazoAprovacaoHoras))]
    [InlineData(nameof(SagaOptions.PrazoPagamentoHoras))]
    [InlineData(nameof(SagaOptions.SweeperIntervaloSegundos))]
    [InlineData(nameof(SagaOptions.SweeperLote))]
    public void Validador_ComValorZeroOuNegativo_DeveRecusarNomeandoAChave(string propriedade)
    {
        foreach (var invalido in new[] { 0, -1 })
        {
            var opcoes = new SagaOptions();
            typeof(SagaOptions).GetProperty(propriedade)!.SetValue(opcoes, invalido);

            var resultado = new ValidadorDasOpcoesDaSaga().Validate(null, opcoes);

            resultado.Failed.Should().BeTrue();
            resultado.Failures.Should().ContainSingle().Which.Should().Be($"Saga:{propriedade} deve ser maior que zero (valor atual: {invalido}).");
        }
    }

    [Fact]
    public void Validador_ComVariosValoresInvalidos_DeveListarTodos()
    {
        var resultado = new ValidadorDasOpcoesDaSaga().Validate(null, new SagaOptions { PrazoAprovacaoHoras = 0, SweeperLote = -5 });

        resultado.Failures.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddServicosDaSaga_ComValorInvalido_DeveDerrubarAPartida()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Saga:PrazoPagamentoHoras"] = "0" });
        builder.Services.AddServicosDaSaga(builder.Configuration);
        using var host = builder.Build();

        var partida = async () => await host.StartAsync();

        (await partida.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain("Saga:PrazoPagamentoHoras");
    }

    [Fact]
    public void AddServicosDaSaga_DeveLerAsVariaveisSagaEntreUnderscores()
    {
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Saga:PrazoAprovacaoHoras"] = "24", ["Saga:PrazoPagamentoHoras"] = "12", ["Saga:SweeperIntervaloSegundos"] = "5",
            ["Saga:SweeperLote"] = "7", ["Saga:SweeperHabilitado"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddServicosDaSaga(configuracao);
        using var provedor = services.BuildServiceProvider();

        var opcoes = provedor.GetRequiredService<IOptions<SagaOptions>>().Value;

        opcoes.Should().BeEquivalentTo(new SagaOptions
        {
            PrazoAprovacaoHoras = 24, PrazoPagamentoHoras = 12, SweeperIntervaloSegundos = 5, SweeperLote = 7, SweeperHabilitado = false
        });
    }

    [Fact]
    public void AddServicosDaSaga_DeveRegistrarPortaSweeperEHostedService()
    {
        var services = new ServiceCollection();
        services.AddServicosDaSaga(new ConfigurationBuilder().Build());

        services.Should().Contain(d => d.ServiceType == typeof(IPrazoRepository) && d.ImplementationType == typeof(PrazoRepository) && d.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(d => d.ServiceType == typeof(SweeperDePrazos) && d.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(SweeperDePrazosBackgroundService));
    }

    [Fact]
    public void AddServicosDaSaga_DeveRegistrarTodasAsActivitiesNoEscopo()
    {
        var services = new ServiceCollection();
        services.AddServicosDaSaga(new ConfigurationBuilder().Build());

        var registradas = services
            .Where(d => d.ImplementationType is { Namespace: "OficinaMecanica.Infrastructure.Saga.Activities" } && d.Lifetime == ServiceLifetime.Scoped)
            .Select(d => d.ServiceType)
            .ToList();

        registradas.Should().Contain(typeof(IniciarSagaActivity)).And.Contain(typeof(ReservarEstoqueActivity))
            .And.Contain(typeof(EstornarPagamentoActivity)).And.Contain(typeof(LiberarEstoqueActivity))
            .And.Contain(typeof(LogarFalhaTecnicaActivity<>));
        registradas.Should().NotContain(typeof(AtividadeDaSaga), "as bases abstratas nao sao activities");
    }

    [Fact]
    public void Estado_DefinirMotivo_DeveAparTruncarERecusarTextoVazio()
    {
        var saga = new OrdemDeServicoSagaState();

        saga.DefinirMotivo("  curto  ");
        saga.MotivoCancelamento.Should().Be("curto");

        saga.DefinirMotivo(new string('x', OrdemDeServico.TamanhoMaximoDoMotivo + 50));
        saga.MotivoCancelamento.Should().HaveLength(OrdemDeServico.TamanhoMaximoDoMotivo);

        saga.DefinirMotivo("   ");
        saga.MotivoCancelamento.Should().BeNull();

        saga.DefinirMotivo(null);
        saga.MotivoCancelamento.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "Execução inviável.")]
    [InlineData("  ", "Execução inviável.")]
    [InlineData(" Peça descontinuada. ", "Execução inviável: Peça descontinuada.")]
    public void Motivos_ExecucaoInviavel_DeveMontarOTexto(string? motivo, string esperado)
    {
        MotivosDaSaga.ExecucaoInviavel(motivo).Should().Be(esperado);
    }

    [Theory]
    [InlineData(null, "Cancelamento solicitado pelo atendimento.")]
    [InlineData("", "Cancelamento solicitado pelo atendimento.")]
    [InlineData(" Cliente desistiu. ", "Cliente desistiu.")]
    public void Motivos_CancelamentoDoStaff_DeveUsarOTextoPadraoQuandoVazio(string? motivo, string esperado)
    {
        MotivosDaSaga.CancelamentoDoStaff(motivo).Should().Be(esperado);
    }

    [Fact]
    public void SagaPrazo_MarcarProcessado_DeveVirarProcessadoEGuardarOsDados()
    {
        var correlacao = Guid.NewGuid();
        var prazoId = Guid.NewGuid();
        var expiraEm = DateTimeOffset.UtcNow;

        var prazo = new SagaPrazo(correlacao, prazoId, TipoPrazo.Pagamento, expiraEm);

        prazo.Processado.Should().BeFalse();
        prazo.MarcarProcessado();
        prazo.Processado.Should().BeTrue();
        (prazo.CorrelationId, prazo.PrazoId, prazo.Tipo, prazo.ExpiraEm).Should().Be((correlacao, prazoId, TipoPrazo.Pagamento, expiraEm));
    }
}
