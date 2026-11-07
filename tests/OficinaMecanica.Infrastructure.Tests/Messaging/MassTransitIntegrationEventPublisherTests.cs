using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OficinaMecanica.Application;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Messaging;

public class MassTransitIntegrationEventPublisherTests
{
    private sealed record MensagemSemMessageId(string Texto);

    private sealed record MensagemComMessageIdDeOutroTipo(string MessageId);

    private static ServiceProvider CriarProvedor()
    {
        var configuracao = ConfiguracaoDeTeste.Criar();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new AmbienteFalso("Testing"));
        services.AddApplication();
        services.AddInfrastructure(configuracao);
        services.AddMensageria(configuracao);
        services.AddMassTransitTestHarness();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task<PublishContext<T>> PublicarAsync<T>(T mensagem) where T : class
    {
        await using var provedor = CriarProvedor();
        var harness = provedor.GetRequiredService<ITestHarness>();
        await harness.Start();

        await using (var escopo = provedor.CreateAsyncScope())
            await escopo.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().PublishAsync(mensagem);

        (await harness.Published.Any<T>()).Should().BeTrue();
        return harness.Published.Select<T>().Single().Context;
    }

    [Fact]
    public async Task PublishAsync_DeveCopiarOMessageIdDoContratoParaOEnvelope()
    {
        var mensagem = new PrazoExpiradoBuilder().DoTipo(TipoPrazo.Aprovacao).Build();

        var envelope = await PublicarAsync(mensagem);

        envelope.MessageId.Should().Be(mensagem.MessageId);
    }

    [Fact]
    public async Task PublishAsync_DeveEntregarOConteudoDoContratoSemAlteracao()
    {
        var mensagem = new ClienteAprovouOrcamentoBuilder().Build();

        var envelope = await PublicarAsync(mensagem);

        envelope.Message.Should().BeEquivalentTo(mensagem);
    }

    [Fact]
    public async Task PublishAsync_ComMessageIdVazio_DeveDeixarOMassTransitGerarOIdDoEnvelope()
    {
        var mensagem = new ClienteAprovouOrcamentoBuilder().ComMessageId(Guid.Empty).Build();

        var envelope = await PublicarAsync(mensagem);

        envelope.MessageId.Should().NotBeNull().And.NotBe(Guid.Empty);
    }

    [Fact]
    public async Task PublishAsync_ContratoSemMessageId_DevePublicarComIdGeradoPeloMassTransit()
    {
        var envelope = await PublicarAsync(new MensagemSemMessageId("sem id"));

        envelope.MessageId.Should().NotBeNull().And.NotBe(Guid.Empty);
    }

    [Fact]
    public async Task PublishAsync_MessageIdQueNaoEGuid_DeveSerIgnorado()
    {
        var envelope = await PublicarAsync(new MensagemComMessageIdDeOutroTipo("nao-e-guid"));

        envelope.MessageId.Should().NotBeNull().And.NotBe(Guid.Empty);
    }

    [Fact]
    public void TodoContratoDeMensagem_DeveTerMessageIdDoTipoGuid()
    {
        var contratos = typeof(Filas).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsPublic: true } && t.Namespace is "OficinaMecanica.Contracts.Events" or "OficinaMecanica.Contracts.Commands")
            .ToList();

        contratos.Should().NotBeEmpty();
        contratos.Should().OnlyContain(
            t => t.GetProperty("MessageId") != null && t.GetProperty("MessageId")!.PropertyType == typeof(Guid),
            "o publisher copia esse campo para o envelope e o inbox deduplica por ele");
    }
}
