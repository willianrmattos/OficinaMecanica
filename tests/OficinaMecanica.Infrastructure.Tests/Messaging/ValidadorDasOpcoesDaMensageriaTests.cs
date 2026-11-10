using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OficinaMecanica.Application;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Messaging;

// Transporte em memoria e persistencia "Nenhuma" so sao aceitos em Development e Testing: em qualquer outro
// ambiente a API precisa do RabbitMQ e do outbox do EF, senao subiria sem broker e perderia mensagem em silencio.
// A guarda e fail-closed: valor fora do enum (o binder aceita "0" ou "9") e recusado em qualquer ambiente, em vez
// de cair no ramo "em memoria". Cada caso varia UMA chave da secao Mensageria e deixa a outra no valor valido.
public class ValidadorDasOpcoesDaMensageriaTests
{
    private static IHost CriarHost(string ambiente, string chave, string valor)
    {
        // A outra chave fica no valor valido de producao; ConfiguracaoDeTeste.Criar poe EmMemoria/Nenhuma como padrao.
        var valores = new Dictionary<string, string>
        {
            ["Mensageria:Transporte"] = "RabbitMq",
            ["Mensageria:Persistencia"] = "EntityFramework",
            [$"Mensageria:{chave}"] = valor
        };

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = ambiente });
        builder.Configuration.AddConfiguration(ConfiguracaoDeTeste.Criar(valores.Select(v => (v.Key, (string?)v.Value)).ToArray()));
        builder.Services.AddLogging();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddMensageria(builder.Configuration);
        return builder.Build();
    }

    private static OpcoesDaMensageria LerOpcoes(IHost host) => host.Services.GetRequiredService<IOptions<OpcoesDaMensageria>>().Value;

    public static TheoryData<string, string, string> ValoresDeTesteForaDeDevelopmentETesting => new()
    {
        { "Production", "Transporte", "EmMemoria" },
        { "Staging", "Transporte", "EmMemoria" },
        { "Production", "Persistencia", "Nenhuma" },
        { "Staging", "Persistencia", "Nenhuma" }
    };

    [Theory]
    [MemberData(nameof(ValoresDeTesteForaDeDevelopmentETesting))]
    public async Task Partida_ForaDeDevelopmentETesting_ComValorDeTesteEmUmaChave_DeveFalharComMensagemEmPortugues(string ambiente, string chave, string valor)
    {
        using var host = CriarHost(ambiente, chave, valor);

        var act = () => host.StartAsync();

        var falha = await act.Should().ThrowAsync<OptionsValidationException>();
        falha.Which.Failures.Should().ContainSingle().Which
            .Should().StartWith($"Mensageria:{chave} deve ser ").And.Contain($"'{ambiente}'").And.Contain("só é permitido em Development e Testing");
    }

    public static TheoryData<string, string, string> ValoresDeTesteEmDevelopmentETesting => new()
    {
        { "Development", "Transporte", "EmMemoria" },
        { "Testing", "Transporte", "EmMemoria" },
        { "Development", "Persistencia", "Nenhuma" },
        { "Testing", "Persistencia", "Nenhuma" }
    };

    [Theory]
    [MemberData(nameof(ValoresDeTesteEmDevelopmentETesting))]
    public void Opcoes_EmDevelopmentOuTesting_ComValorDeTesteEmUmaChave_DevemSerValidas(string ambiente, string chave, string valor)
    {
        using var host = CriarHost(ambiente, chave, valor);

        var opcoes = LerOpcoes(host);

        (chave == "Transporte" ? opcoes.Transporte.ToString() : opcoes.Persistencia.ToString()).Should().Be(valor);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Opcoes_ForaDeDevelopmentETesting_ComRabbitMqEOutboxDoEf_DevemSerValidas(string ambiente)
    {
        using var host = CriarHost(ambiente, "Transporte", "RabbitMq");

        var opcoes = LerOpcoes(host);

        opcoes.Transporte.Should().Be(TransporteDaMensageria.RabbitMq);
        opcoes.Persistencia.Should().Be(PersistenciaDaMensageria.EntityFramework);
    }

    public static TheoryData<string, string, string> ValoresForaDoEnum
    {
        get
        {
            var dados = new TheoryData<string, string, string>();
            foreach (var ambiente in new[] { "Production", "Staging", "Development", "Testing" })
                foreach (var chave in new[] { "Transporte", "Persistencia" })
                    foreach (var valor in new[] { "0", "9" })
                        dados.Add(ambiente, chave, valor);
            return dados;
        }
    }

    [Theory]
    [MemberData(nameof(ValoresForaDoEnum))]
    public void AddMensageria_ComNumeroForaDoEnumEmUmaChave_DeveLancarEmQualquerAmbienteEmVezDeCairEmMemoria(string ambiente, string chave, string valor)
    {
        var act = () => CriarHost(ambiente, chave, valor);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Mensageria:{chave} tem o valor inválido '{valor}'. Valores aceitos: *");
    }

    public static TheoryData<string, string> TextoQueNaoEValorDoEnum
    {
        get
        {
            var dados = new TheoryData<string, string>();
            foreach (var ambiente in new[] { "Production", "Staging", "Development", "Testing" })
                foreach (var chave in new[] { "Transporte", "Persistencia" })
                    dados.Add(ambiente, chave);
            return dados;
        }
    }

    [Theory]
    [MemberData(nameof(TextoQueNaoEValorDoEnum))]
    public void AddMensageria_ComTextoQueNaoEValorDoEnumEmUmaChave_DeveLancarComMensagemEmPortuguesQueCitaAChave(string ambiente, string chave)
    {
        var act = () => CriarHost(ambiente, chave, "texto-invalido");

        var falha = act.Should().Throw<InvalidOperationException>();
        falha.Which.Message.Should().StartWith("A seção Mensageria da configuração tem um valor inválido.").And.Contain($"Mensageria:{chave}");
        falha.Which.InnerException.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Validador_ComTransporteForaDoEnum_DeveFalharEmQualquerAmbiente(string ambiente)
    {
        var validador = new ValidadorDasOpcoesDaMensageria(new AmbienteFalso(ambiente));

        var resultado = validador.Validate(null, new OpcoesDaMensageria { Transporte = (TransporteDaMensageria)9 });

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle().Which.Should().Be(
            OpcoesDaMensageria.MensagemDeValorInvalido("Transporte", 9, OpcoesDaMensageria.TransportesAceitos));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Validador_ComPersistenciaForaDoEnum_DeveFalharEmQualquerAmbiente(string ambiente)
    {
        var validador = new ValidadorDasOpcoesDaMensageria(new AmbienteFalso(ambiente));

        var resultado = validador.Validate(null, new OpcoesDaMensageria { Persistencia = (PersistenciaDaMensageria)0 });

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle().Which.Should().Be(
            OpcoesDaMensageria.MensagemDeValorInvalido("Persistencia", 0, OpcoesDaMensageria.PersistenciasAceitas));
    }

    [Fact]
    public void Validador_EmProducaoComAsDuasChavesDeTeste_DeveReportarAsDuasFalhas()
    {
        var validador = new ValidadorDasOpcoesDaMensageria(new AmbienteFalso("Production"));

        var resultado = validador.Validate(null, new OpcoesDaMensageria
        {
            Transporte = TransporteDaMensageria.EmMemoria,
            Persistencia = PersistenciaDaMensageria.Nenhuma
        });

        resultado.Failures.Should().HaveCount(2);
    }

    [Fact]
    public void OpcoesDaMensageria_ComValoresDefinidos_NaoDeveLancarAoDecidirORamo()
    {
        var emMemoria = new OpcoesDaMensageria { Transporte = TransporteDaMensageria.EmMemoria, Persistencia = PersistenciaDaMensageria.Nenhuma };

        emMemoria.UsaRabbitMq.Should().BeFalse();
        emMemoria.UsaOutboxDoEntityFramework.Should().BeFalse();
        new OpcoesDaMensageria().UsaRabbitMq.Should().BeTrue();
        new OpcoesDaMensageria().UsaOutboxDoEntityFramework.Should().BeTrue();
    }

    [Fact]
    public void OpcoesDaMensageria_ComValoresIndefinidos_DeveLancarAoDecidirORamo()
    {
        var indefinidas = new OpcoesDaMensageria { Transporte = (TransporteDaMensageria)9, Persistencia = (PersistenciaDaMensageria)9 };

        var transporte = () => indefinidas.UsaRabbitMq;
        var persistencia = () => indefinidas.UsaOutboxDoEntityFramework;

        transporte.Should().Throw<InvalidOperationException>().WithMessage("Mensageria:Transporte tem o valor inválido '9'*");
        persistencia.Should().Throw<InvalidOperationException>().WithMessage("Mensageria:Persistencia tem o valor inválido '9'*");
    }
}
