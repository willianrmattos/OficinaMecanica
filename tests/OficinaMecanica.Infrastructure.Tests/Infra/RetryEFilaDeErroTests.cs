using FluentAssertions;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// O que o UseMessageRetry repete e o que vai direto para a fila _error: regra de negocio violada, conflito de estado
// e validacao reprovada nao mudam de resultado se a mensagem for repetida; erro tecnico merece todas as tentativas.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class RetryEFilaDeErroTests
{
    private const string Fila = "os-f3a-consumer-que-falha";
    private const string FilaDeErro = Fila + "_error";

    // O ambiente de teste tem tres intervalos de retry (AmbienteDeInfra): uma execucao inicial e tres repeticoes.
    private const int ExecucoesComRetryEsgotado = 4;

    private readonly InfraFixture _infra;

    public RetryEFilaDeErroTests(InfraFixture infra) => _infra = infra;

    private Task<AmbienteDeInfra> CriarAmbienteAsync() =>
        AmbienteDeInfra.CriarAsync(
            _infra,
            x => x.AddConsumer<ConsumerQueLanca>(),
            (contexto, cfg) => cfg.ReceiveEndpoint(Fila, endpoint =>
            {
                endpoint.ConfigurarRetryEOutbox(contexto);
                endpoint.ConfigureConsumer<ConsumerQueLanca>(contexto);
            }));

    private Task<bool> EsperarMensagemNaFilaDeErroAsync(AmbienteDeInfra ambiente) =>
        Esperar.AteAsync(async () => await _infra.MensagensNaFilaAsync(ambiente.VirtualHost, FilaDeErro) == 1, TimeSpan.FromSeconds(30));

    public static TheoryData<string, Func<Exception>> ExcecoesQueNaoSaoRepetidas => new()
    {
        { nameof(DomainException), () => new DomainException("Regra de negócio violada.") },
        { nameof(ConflitoException), () => new ConflitoException("A OS não está no estado esperado.") },
        { nameof(ValidationException), () => new ValidationException("Validação reprovada.") }
    };

    [Theory]
    [MemberData(nameof(ExcecoesQueNaoSaoRepetidas))]
    public async Task ConsumerQueLancaExcecaoDeNegocio_DeveRodarUmaVezEIrParaAFilaDeErro(string tipo, Func<Exception> fabrica)
    {
        await using var ambiente = await CriarAmbienteAsync();
        ambiente.Consumo.FabricaDeExcecao = fabrica;

        await ambiente.Servicos.GetRequiredService<IBus>().Publish(new MensagemDeTeste(Guid.NewGuid(), Guid.NewGuid()));

        (await EsperarMensagemNaFilaDeErroAsync(ambiente)).Should().BeTrue($"{tipo} vai direto para a fila _error");
        ambiente.Consumo.Consumos.Should().ContainSingle($"{tipo} nao e repetida pelo UseMessageRetry");
    }

    [Fact]
    public async Task ConsumerQueLancaFalhaTecnica_DeveEsgotarOsIntervalosDeRetryAntesDeIrParaAFilaDeErro()
    {
        await using var ambiente = await CriarAmbienteAsync();
        ambiente.Consumo.FabricaDeExcecao = () => new FalhaTecnicaSimuladaException();

        await ambiente.Servicos.GetRequiredService<IBus>().Publish(new MensagemDeTeste(Guid.NewGuid(), Guid.NewGuid()));

        (await EsperarMensagemNaFilaDeErroAsync(ambiente)).Should().BeTrue("depois da ultima repeticao a mensagem vai para a fila _error");
        ambiente.Consumo.Consumos.Select(c => c.Tentativa).Should().Equal(Enumerable.Range(1, ExecucoesComRetryEsgotado));
    }
}
