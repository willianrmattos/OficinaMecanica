using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// O broker entrega "pelo menos uma vez": a mesma mensagem (mesmo MessageId) pode chegar duas vezes, e o inbox
// do consumer e o que garante que o efeito acontece uma so.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class InboxDeduplicacaoTests
{
    private const string Fila = "os-f3a-mensagens-de-teste";
    private static readonly TimeSpan JanelaSemNovasEntregas = TimeSpan.FromSeconds(3);

    private readonly InfraFixture _infra;

    public InboxDeduplicacaoTests(InfraFixture infra) => _infra = infra;

    private Task<AmbienteDeInfra> CriarAmbienteAsync() =>
        AmbienteDeInfra.CriarAsync(
            _infra,
            x => x.AddConsumer<ConsumerDeMensagemDeTeste>(),
            (contexto, cfg) => cfg.ReceiveEndpoint(Fila, endpoint =>
            {
                endpoint.ConfigurarRetryEOutbox(contexto);
                endpoint.ConfigureConsumer<ConsumerDeMensagemDeTeste>(contexto);
            }));

    private static Task PublicarComMessageIdAsync(IPublishEndpoint publicador, MensagemDeTeste mensagem) =>
        publicador.Publish(mensagem, Pipe.Execute<PublishContext<MensagemDeTeste>>(contexto => contexto.MessageId = mensagem.MessageId));

    [Fact]
    public async Task MensagemDuplicadaEmParalelo_DeveSerConsumidaUmaSoVez()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var mensagem = new MensagemDeTeste(Guid.NewGuid(), Guid.NewGuid());
        var publicador = ambiente.Servicos.GetRequiredService<IBus>();

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => PublicarComMessageIdAsync(publicador, mensagem)));

        (await Esperar.AteAsync(() => ambiente.Consumo.Consumos.Count >= 1)).Should().BeTrue();
        await Task.Delay(JanelaSemNovasEntregas);
        ambiente.Consumo.Consumos.Should().ContainSingle().Which.MessageIdDoEnvelope.Should().Be(mensagem.MessageId);
    }

    [Fact]
    public async Task MensagemRepetidaDepoisDeConsumida_NaoDeveSerConsumidaDeNovo()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var mensagem = new MensagemDeTeste(Guid.NewGuid(), Guid.NewGuid());
        var publicador = ambiente.Servicos.GetRequiredService<IBus>();

        await PublicarComMessageIdAsync(publicador, mensagem);
        (await Esperar.AteAsync(() => ambiente.Consumo.Consumos.Count == 1)).Should().BeTrue();

        await PublicarComMessageIdAsync(publicador, mensagem);
        await Task.Delay(JanelaSemNovasEntregas);

        ambiente.Consumo.Consumos.Should().ContainSingle();
        var inbox = await ambiente.ConsultarAsync(db => db.Set<InboxState>().Where(i => i.MessageId == mensagem.MessageId).ToListAsync());
        inbox.Should().ContainSingle().Which.Consumed.Should().NotBeNull("o inbox registra que o consumer terminou");
    }

    [Fact]
    public async Task MensagensComMessageIdDiferente_DevemSerConsumidasCadaUma()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var publicador = ambiente.Servicos.GetRequiredService<IBus>();

        await PublicarComMessageIdAsync(publicador, new MensagemDeTeste(Guid.NewGuid(), Guid.NewGuid()));
        await PublicarComMessageIdAsync(publicador, new MensagemDeTeste(Guid.NewGuid(), Guid.NewGuid()));

        (await Esperar.AteAsync(() => ambiente.Consumo.Consumos.Count == 2)).Should().BeTrue();
    }
}
