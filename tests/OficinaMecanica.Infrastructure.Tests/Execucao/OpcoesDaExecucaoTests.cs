using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Execucao;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Execucao;

public class OpcoesDaExecucaoTests
{
    [Fact]
    public void Padroes_DevemApontarParaAExecucaoLocalELimitarACurtoPrazo()
    {
        var opcoes = new OpcoesDaExecucao();

        opcoes.BaseUrl.Should().Be("http://localhost:5002");
        opcoes.TimeoutSegundos.Should().Be(6);
        opcoes.TimeoutPorTentativaSegundos.Should().Be(2);
        opcoes.TentativasDeRetry.Should().Be(2);
        opcoes.AtrasoDoRetryMs.Should().Be(200);
        opcoes.CircuitoTaxaDeFalha.Should().Be(0.5);
        opcoes.CircuitoMinimoDeChamadas.Should().Be(5);
        opcoes.CircuitoJanelaSegundos.Should().Be(30);
        opcoes.CircuitoPausaSegundos.Should().Be(30);
        OpcoesDaExecucao.Secao.Should().Be("Execucao");
        new ValidadorDasOpcoesDaExecucao().Validate(null, opcoes).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("execucao")]
    [InlineData("/api/execucao")]
    [InlineData("ftp://execucao.teste")]
    public void Validador_BaseUrlQueNaoEHttpAbsoluta_DeveRecusarNomeandoAChave(string baseUrl)
    {
        var resultado = new ValidadorDasOpcoesDaExecucao().Validate(null, new OpcoesDaExecucao { BaseUrl = baseUrl });

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle().Which.Should().StartWith("Execucao:BaseUrl deve ser uma URL absoluta http ou https");
    }

    [Theory]
    [InlineData("http://oficinamecanica-execucao")]
    [InlineData("https://apimfiap.azure-api.net/execucaoserver")]
    [InlineData("http://host.docker.internal:5002")]
    public void Validador_BaseUrlHttpAbsoluta_DeveAceitar(string baseUrl) =>
        new ValidadorDasOpcoesDaExecucao().Validate(null, new OpcoesDaExecucao { BaseUrl = baseUrl }).Succeeded.Should().BeTrue();

    [Theory]
    [InlineData(nameof(OpcoesDaExecucao.TimeoutSegundos))]
    [InlineData(nameof(OpcoesDaExecucao.TimeoutPorTentativaSegundos))]
    [InlineData(nameof(OpcoesDaExecucao.TentativasDeRetry))]
    [InlineData(nameof(OpcoesDaExecucao.AtrasoDoRetryMs))]
    [InlineData(nameof(OpcoesDaExecucao.CircuitoJanelaSegundos))]
    [InlineData(nameof(OpcoesDaExecucao.CircuitoPausaSegundos))]
    public void Validador_ValorZeroOuNegativo_DeveRecusarNomeandoAChave(string propriedade)
    {
        foreach (var invalido in new[] { 0, -1 })
        {
            var opcoes = new OpcoesDaExecucao { TimeoutSegundos = 10 };
            typeof(OpcoesDaExecucao).GetProperty(propriedade)!.SetValue(opcoes, invalido);

            var resultado = new ValidadorDasOpcoesDaExecucao().Validate(null, opcoes);

            resultado.Failed.Should().BeTrue();
            resultado.Failures.Should().Contain($"Execucao:{propriedade} deve ser maior que zero (valor atual: {invalido}).");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Validador_MinimoDeChamadasDoCircuitoAbaixoDeDois_DeveRecusar(int minimo)
    {
        var resultado = new ValidadorDasOpcoesDaExecucao().Validate(null, new OpcoesDaExecucao { CircuitoMinimoDeChamadas = minimo });

        resultado.Failures.Should().ContainSingle().Which.Should().Be($"Execucao:CircuitoMinimoDeChamadas deve ser pelo menos 2 (valor atual: {minimo}).");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Validador_TaxaDeFalhaForaDeZeroAUm_DeveRecusar(double taxa)
    {
        var resultado = new ValidadorDasOpcoesDaExecucao().Validate(null, new OpcoesDaExecucao { CircuitoTaxaDeFalha = taxa });

        resultado.Failures.Should().ContainSingle().Which.Should().StartWith("Execucao:CircuitoTaxaDeFalha deve estar entre 0 (exclusivo) e 1");
    }

    [Fact]
    public void Validador_TaxaDeFalhaUm_DeveAceitar() =>
        new ValidadorDasOpcoesDaExecucao().Validate(null, new OpcoesDaExecucao { CircuitoTaxaDeFalha = 1 }).Succeeded.Should().BeTrue();

    [Fact]
    public void Validador_TimeoutDeTentativaMaiorQueOTotal_DeveRecusar()
    {
        var resultado = new ValidadorDasOpcoesDaExecucao().Validate(null, new OpcoesDaExecucao { TimeoutSegundos = 3, TimeoutPorTentativaSegundos = 4 });

        resultado.Failures.Should().ContainSingle().Which.Should().Be("Execucao:TimeoutPorTentativaSegundos (4) não pode passar de TimeoutSegundos (3).");
    }

    [Fact]
    public void Validador_VariosValoresInvalidos_DeveListarTodos()
    {
        var resultado = new ValidadorDasOpcoesDaExecucao().Validate(null, new OpcoesDaExecucao { BaseUrl = "x", TentativasDeRetry = 0, CircuitoMinimoDeChamadas = 1 });

        resultado.Failures.Should().HaveCount(3);
    }

    [Fact]
    public async Task Partida_ComOpcaoInvalida_DeveFalharNoStartPorCausaDoValidateOnStart()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(servicos => servicos.AddClienteDaExecucao(ConfiguracaoDeTeste.Criar(("Execucao:BaseUrl", "nao-e-url"), ("Execucao:TentativasDeRetry", "0"))))
            .Build();

        var act = async () => await host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().HaveCount(2).And.Contain(f => f.StartsWith("Execucao:BaseUrl"));
    }

    [Fact]
    public async Task Partida_ComOpcoesValidas_DeveSubirEResolverOClienteDaPorta()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(servicos => servicos.AddClienteDaExecucao(ConfiguracaoDeTeste.Criar(("Execucao:BaseUrl", "http://execucao.teste"))))
            .Build();

        await host.StartAsync();

        host.Services.GetRequiredService<IExecucaoClient>().Should().BeOfType<ExecucaoHttpClient>();
        host.Services.GetRequiredService<IOptions<OpcoesDaExecucao>>().Value.BaseUrl.Should().Be("http://execucao.teste");
        await host.StopAsync();
    }

    [Fact]
    public async Task Configuracao_DeveLerAsVariaveisExecucaoDoAmbienteComoDoAppsettings()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(servicos => servicos.AddClienteDaExecucao(ConfiguracaoDeTeste.Criar(
                ("Execucao:BaseUrl", "http://oficinamecanica-execucao"),
                ("Execucao:TimeoutSegundos", "9"),
                ("Execucao:TimeoutPorTentativaSegundos", "3"),
                ("Execucao:TentativasDeRetry", "4"),
                ("Execucao:AtrasoDoRetryMs", "50"),
                ("Execucao:CircuitoTaxaDeFalha", "0.75"),
                ("Execucao:CircuitoMinimoDeChamadas", "8"),
                ("Execucao:CircuitoJanelaSegundos", "60"),
                ("Execucao:CircuitoPausaSegundos", "45"))))
            .Build();
        await host.StartAsync();

        var opcoes = host.Services.GetRequiredService<IOptions<OpcoesDaExecucao>>().Value;

        opcoes.Should().BeEquivalentTo(new OpcoesDaExecucao
        {
            BaseUrl = "http://oficinamecanica-execucao", TimeoutSegundos = 9, TimeoutPorTentativaSegundos = 3, TentativasDeRetry = 4,
            AtrasoDoRetryMs = 50, CircuitoTaxaDeFalha = 0.75, CircuitoMinimoDeChamadas = 8, CircuitoJanelaSegundos = 60, CircuitoPausaSegundos = 45
        });
        await host.StopAsync();
    }
}
