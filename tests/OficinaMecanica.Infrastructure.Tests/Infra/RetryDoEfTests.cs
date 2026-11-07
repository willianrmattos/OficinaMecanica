using FluentAssertions;
using Microsoft.Data.SqlClient;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// O EnableRetryOnFailure so vale fora de consumer: dentro dele a transacao e do MassTransit e quem repete e o
// UseMessageRetry.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class RetryDoEfTests
{
    private const string Fila = "os-f3a-diagnostico";

    private readonly InfraFixture _infra;

    public RetryDoEfTests(InfraFixture infra) => _infra = infra;

    private Task<AmbienteDeInfra> CriarAmbienteAsync() =>
        AmbienteDeInfra.CriarAsync(
            _infra,
            x => x.AddConsumer<ConsumerDeDiagnostico>(),
            (contexto, cfg) => cfg.ReceiveEndpoint(Fila, endpoint =>
            {
                endpoint.ConfigurarRetryEOutbox(contexto);
                endpoint.ConfigureConsumer<ConsumerDeDiagnostico>(contexto);
            }));

    private static async Task<OrdemDeServico> SemearOrdemRecebidaAsync(AmbienteDeInfra ambiente)
    {
        var cliente = new ClienteBuilder().Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();
        var ordem = new OrdemDeServicoBuilder().DoCliente(cliente.Id).DoVeiculo(veiculo.Id).Recebida();
        await ambiente.SemearAsync(cliente, veiculo, ordem);
        return ordem;
    }

    private static Task<bool> OrdemEstaEmDiagnosticoAsync(AmbienteDeInfra ambiente, Guid ordemId) =>
        Esperar.AteAsync(async () =>
            await ambiente.ConsultarAsync(db => db.OrdensDeServico.Where(o => o.Id == ordemId).Select(o => o.Status).SingleAsync())
                == StatusOrdemDeServico.EmDiagnostico);

    [Fact]
    public async Task Consumer_DeveEscreverNoBancoSemExcecaoDeEstrategiaDeExecucao()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await ambiente.Servicos.GetRequiredService<IBus>().Publish(new MensagemDeTeste(Guid.NewGuid(), ordem.Id));

        (await Esperar.AteAsync(() => ambiente.Consumo.Consumos.Count >= 1)).Should().BeTrue();
        (await OrdemEstaEmDiagnosticoAsync(ambiente, ordem.Id)).Should().BeTrue("o consumer gravou o novo status dentro da transacao do MassTransit");

        var consumo = ambiente.Consumo.Consumos.Should().ContainSingle().Subject;
        consumo.TransacaoAberta.Should().BeTrue("o outbox do consumer abre a transacao antes de chamar o consumer");
        consumo.EstrategiaDeExecucao.Should().NotBe(typeof(SqlServerRetryingExecutionStrategy), "dentro de consumer a estrategia de execucao configurada nao repete");
        ambiente.Logs.Excecoes().Should().NotContain(e => e is InvalidOperationException);
    }

    [Fact]
    public async Task ForaDeConsumer_RetryDoEfDeveFicarAtivo()
    {
        await using var ambiente = await CriarAmbienteAsync();
        await using var escopo = ambiente.CriarEscopo();

        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.CreateExecutionStrategy().Should().BeOfType<SqlServerRetryingExecutionStrategy>("o caminho HTTP precisa tolerar o auto-pause do Azure SQL serverless");
    }

    [Fact]
    public async Task ControleNegativo_RetryDoEfComTransacaoDoChamadorForaDaEstrategia_LancaInvalidOperation()
    {
        await using var ambiente = await CriarAmbienteAsync();
        await using var escopo = ambiente.CriarEscopo();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transacao = await db.Database.BeginTransactionAsync();
        db.Add(new ClienteBuilder().Build());

        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("user-initiated transactions");
    }

    [Fact]
    public async Task Consumer_ComConexaoDerrubadaDepoisDeEscrever_DeveSerRecuperadoPeloMessageRetrySemReplayDoEf()
    {
        await using var ambiente = await CriarAmbienteAsync();
        ambiente.Consumo.DerrubarConexaoUmaVez = true;
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await ambiente.Servicos.GetRequiredService<IBus>().Publish(new MensagemDeTeste(Guid.NewGuid(), ordem.Id));

        (await OrdemEstaEmDiagnosticoAsync(ambiente, ordem.Id)).Should().BeTrue("a segunda tentativa, em escopo e transacao novos, confirma");
        ambiente.Consumo.Consumos.Select(c => c.Tentativa).Should().Equal(new[] { 1, 2 }, "o consumer inteiro roda de novo pelo UseMessageRetry, e nao so o save pelo EF");
        ambiente.Consumo.Consumos.Should().OnlyContain(c => c.EstrategiaDeExecucao != typeof(SqlServerRetryingExecutionStrategy));
        (await Esperar.AteAsync(() => ambiente.Emails.Enviados.Count >= 1)).Should().BeTrue();
        await Task.Delay(TimeSpan.FromSeconds(2));
        ambiente.Emails.Enviados.Should().ContainSingle("a tentativa que perdeu a conexao nao confirmou e nao manda e-mail");

        // Sem retry do EF, o erro de conexao sobe (as vezes embrulhado pelo EF, as vezes cru) para o UseMessageRetry;
        // nenhuma excecao e de "transacao iniciada pelo usuario", que e o que o retry do EF lancaria.
        var excecoes = ambiente.Logs.Excecoes().ToList();
        excecoes.Should().Contain(e => e is SqlException || e.InnerException is SqlException, "o erro de conexao chegou ao MassTransit em vez de ser repetido pelo EF");
        excecoes.Should().NotContain(e => e.Message.Contains("user-initiated transactions"));
    }

    [Fact]
    public async Task Consumer_ComFalhaTransitoria_DeveSerRepetidoPeloMessageRetryAteConcluir()
    {
        await using var ambiente = await CriarAmbienteAsync();
        ambiente.Consumo.TentativasQueFalhamDepoisDeEscrever = 2;
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await ambiente.Servicos.GetRequiredService<IBus>().Publish(new MensagemDeTeste(Guid.NewGuid(), ordem.Id));

        (await Esperar.AteAsync(() => ambiente.Consumo.Consumos.Count == 3)).Should().BeTrue("duas falhas e uma tentativa que conclui");
        (await OrdemEstaEmDiagnosticoAsync(ambiente, ordem.Id)).Should().BeTrue();
        ambiente.Consumo.Consumos.Select(c => c.Tentativa).Should().Equal(1, 2, 3);
        ambiente.Consumo.Consumos.Should().OnlyContain(c => c.EstrategiaDeExecucao != typeof(SqlServerRetryingExecutionStrategy));
        ambiente.Logs.Excecoes().Should().NotContain(e => e is InvalidOperationException);
    }
}
