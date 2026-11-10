using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Saga.Activities;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// Pecas das activities que o harness nao exercita por si so: o caminho da base sem dado de evento (que o MassTransit so usa em
// comportamento sem mensagem) e a recusa de enviar comando sem MessageId.
public class AtividadesDaSagaTests
{
    private sealed class AtividadeDeTeste : AtividadeDaSaga
    {
        public AtividadeDeTeste(TimeProvider relogio) : base(relogio)
        {
        }

        public int Execucoes { get; private set; }

        protected override Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto)
        {
            Execucoes++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Execute_SemDadoDeEvento_DeveCarimbarAtualizadaEmExecutarOPassoEPassarAoProximo()
    {
        var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));
        var saga = new OrdemDeServicoSagaState { CorrelationId = Guid.NewGuid() };
        var contexto = new Mock<BehaviorContext<OrdemDeServicoSagaState>>();
        contexto.SetupGet(c => c.Saga).Returns(saga);
        var proximo = new Mock<IBehavior<OrdemDeServicoSagaState>>();
        proximo.Setup(p => p.Execute(contexto.Object)).Returns(Task.CompletedTask);
        var atividade = new AtividadeDeTeste(relogio);

        await atividade.Execute(contexto.Object, proximo.Object);

        atividade.Execucoes.Should().Be(1);
        saga.AtualizadaEm.Should().Be(relogio.GetUtcNow());
        proximo.Verify(p => p.Execute(contexto.Object), Times.Once);
    }

    [Fact]
    public async Task EnviarAsync_ComandoSemMessageId_DeveRecusarSemEnviarNada()
    {
        var contexto = new Mock<ConsumeContext>();
        var comando = new CancelarOrcamento(Guid.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow, null, "Motivo.");

        var envio = async () => await contexto.Object.EnviarAsync(Filas.Billing, comando);

        (await envio.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("CancelarOrcamento").And.Contain("MessageId");
        contexto.Verify(c => c.GetSendEndpoint(It.IsAny<Uri>()), Times.Never);
    }
}
