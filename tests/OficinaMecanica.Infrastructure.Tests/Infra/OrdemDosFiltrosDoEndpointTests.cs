using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// Efeito observavel do pipeline do endpoint (retry por fora, outbox por dentro): cada tentativa comeca com o outbox
// vazio, entao so a publicacao da tentativa que confirma sai do banco. A ordem dos filtros em si e lida do probe do
// bus em ConfigurarRetryEOutboxTests: o MassTransit 8.5.12 monta o retry por fora mesmo que o outbox seja configurado antes.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class OrdemDosFiltrosDoEndpointTests
{
    private const string FilaDoPublicador = "os-f3a-consumer-que-publica";
    private const string FilaDosPrazos = "os-f3a-prazos-observados";
    private static readonly TimeSpan JanelaSemNovasEntregas = TimeSpan.FromSeconds(3);

    private readonly InfraFixture _infra;

    public OrdemDosFiltrosDoEndpointTests(InfraFixture infra) => _infra = infra;

    private Task<AmbienteDeInfra> CriarAmbienteAsync() =>
        AmbienteDeInfra.CriarAsync(
            _infra,
            x =>
            {
                x.AddConsumer<ConsumerQuePublica>();
                x.AddConsumer<ConsumerDePrazoExpirado>();
            },
            (contexto, cfg) =>
            {
                cfg.ReceiveEndpoint(FilaDoPublicador, endpoint =>
                {
                    endpoint.ConfigurarRetryEOutbox(contexto);
                    endpoint.ConfigureConsumer<ConsumerQuePublica>(contexto);
                });

                cfg.ReceiveEndpoint(FilaDosPrazos, endpoint =>
                {
                    endpoint.ConfigurarRetryEOutbox(contexto);
                    endpoint.ConfigureConsumer<ConsumerDePrazoExpirado>(contexto);
                });
            });

    [Fact]
    public async Task ConsumerQuePublicaEFalhaDuasVezes_DeveEntregarUmaSoPublicacaoESemLinhasOrfasNoOutbox()
    {
        await using var ambiente = await CriarAmbienteAsync();
        ambiente.Consumo.TentativasQueFalhamDepoisDeEscrever = 2;

        await ambiente.Servicos.GetRequiredService<IBus>().Publish(new MensagemDeTeste(Guid.NewGuid(), Guid.NewGuid()));

        (await Esperar.AteAsync(() => ambiente.Consumo.PrazosRecebidos.Count >= 1)).Should().BeTrue("a tentativa que confirmou entrega a publicacao");
        await Task.Delay(JanelaSemNovasEntregas);

        ambiente.Consumo.Consumos.Select(c => c.Tentativa).Should().Equal(1, 2, 3);
        ambiente.Consumo.PrazosRecebidos.Should().ContainSingle("as duas tentativas desfeitas nao deixam publicacao no outbox");
        (await ambiente.ConsultarAsync(db => db.Set<OutboxMessage>().CountAsync())).Should().Be(0, "o que foi entregue sai do outbox e nada fica orfao");
    }

    [Fact]
    public async Task ConsumerQuePublicaEPerdeAConexaoDoBanco_DeveRecuperarEmEscopoNovoEntregandoUmaSoPublicacao()
    {
        await using var ambiente = await CriarAmbienteAsync();
        ambiente.Consumo.DerrubarConexaoUmaVez = true;

        await ambiente.Servicos.GetRequiredService<IBus>().Publish(new MensagemDeTeste(Guid.NewGuid(), Guid.NewGuid()));

        // A segunda tentativa abre conexao e transacao novas: a primeira morreu junto com a sessao do banco.
        (await Esperar.AteAsync(() => ambiente.Consumo.PrazosRecebidos.Count >= 1)).Should().BeTrue("a segunda tentativa confirma e entrega");
        await Task.Delay(JanelaSemNovasEntregas);

        ambiente.Consumo.Consumos.Select(c => c.Tentativa).Should().Equal(1, 2);
        ambiente.Consumo.PrazosRecebidos.Should().ContainSingle();
        (await ambiente.ConsultarAsync(db => db.Set<OutboxMessage>().CountAsync())).Should().Be(0);
        (await _infra.MensagensNaFilaAsync(ambiente.VirtualHost, FilaDoPublicador + "_error")).Should().Be(0);
    }
}
