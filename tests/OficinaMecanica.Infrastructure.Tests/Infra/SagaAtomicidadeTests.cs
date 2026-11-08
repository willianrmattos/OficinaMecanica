using FluentAssertions;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// Uma transicao da saga e UMA transacao: OS, estoque, saga, prazo e mensagens do outbox comitam juntos ou nao comitam. Nao existe rollback
// manual: a falha desfaz tudo e o retry do endpoint reprocessa a mensagem do zero.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class SagaAtomicidadeTests
{
    private static readonly (string, string?)[] RetryRapido =
    {
        ("Mensageria:RetryIntervalosMs:0", "100"), ("Mensageria:RetryIntervalosMs:1", "100"), ("Mensageria:RetryIntervalosMs:2", "100")
    };

    private readonly InfraFixture _infra;

    public SagaAtomicidadeTests(InfraFixture infra) => _infra = infra;

    [Fact]
    public async Task ExcecaoDentroDaActivityDepoisDaEscrita_NaoDevePersistirOsEstoqueSagaPrazoNemMensagem()
    {
        var falhas = new FalhasInjetadas();
        await using var ambiente = await AmbienteDaSagaInfra.CriarAsync(
            _infra,
            servicos =>
            {
                servicos.AddSingleton(falhas);
                servicos.AddTransient<IPipelineBehavior<OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento.AplicarAprovacaoDoOrcamentoCommand, Unit>, FalhaDepoisDoHandlerDeAprovacaoBehavior>();
            },
            true,
            RetryRapido);
        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");
        var emailsAntes = ambiente.Infra.Emails.Enviados.Count;
        falhas.FalharDepoisDoHandlerDeAprovacao = true;

        await ambiente.AprovarAsync(ordem.OrdemId);

        // A mensagem esgota o retry (1 tentativa + 3) e vai para _error; a transacao de cada tentativa foi desfeita.
        (await Esperar.AteAsync(async () => await _infra.MensagensNaFilaAsync(ambiente.Infra.VirtualHost, "os-saga_error") == 1, TimeSpan.FromSeconds(40)))
            .Should().BeTrue("a falha tecnica esgota o retry e vai para a fila de erro");

        (await ambiente.EstadoDaSagaAsync(ordem.OrdemId)).Should().Be("AguardandoAprovacao", "a saga nao mudou de estado");
        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao, "o handler gravou AguardandoPagamento dentro da transacao, que voltou");
        os.EstoqueReservado.Should().BeFalse("a reserva de estoque tambem voltou");
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial);
        (await ambiente.Infra.ConsultarAsync(db => db.SagaPrazos.CountAsync(p => p.CorrelationId == ordem.OrdemId))).Should().Be(1, "so o prazo de aprovacao: o de pagamento voltou");
        (await ambiente.Infra.ConsultarAsync(db => db.Set<OutboxMessage>().CountAsync(m => m.Body!.Contains("SolicitarPagamento")))).Should().Be(0);
        ambiente.Servicos.NoBilling<SolicitarPagamento>(ordem.OrdemId).Should().Be(0);
        ambiente.Infra.Emails.Enviados.Count.Should().Be(emailsAntes, "e-mail so sai depois do commit: nenhuma tentativa desfeita manda e-mail");
    }

    [Fact]
    public async Task FalhaNoCommitDaSaga_DeveDesfazerTudoEORetryNaoDeveDuplicarNenhumEfeito()
    {
        var falhas = new FalhasInjetadas();
        await using var ambiente = await AmbienteDaSagaInfra.CriarAsync(
            _infra,
            servicos =>
            {
                servicos.AddSingleton(falhas);
                servicos.AddDbContext<AppDbContext>(opcoes => opcoes.AddInterceptors(new FalhaNoCommitDaSagaInterceptor(falhas)));
            },
            true,
            RetryRapido);
        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");
        var emailsAntes = ambiente.Infra.Emails.Enviados.Count;
        falhas.FalharUmCommitDaSagaEmAguardandoPagamento = true;

        await ambiente.AprovarAsync(ordem.OrdemId);

        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoPagamento");
        (await Esperar.AteAsync(() => ambiente.Servicos.NoBilling<SolicitarPagamento>(ordem.OrdemId) >= 1)).Should().BeTrue();
        await Task.Delay(TimeSpan.FromSeconds(3));

        falhas.CommitsQueFalharam.Should().BeGreaterThanOrEqualTo(1, "o commit da primeira tentativa falhou de verdade");
        ambiente.Servicos.NoBilling<SolicitarPagamento>(ordem.OrdemId).Should().Be(1, "as mensagens da tentativa desfeita voltaram com o rollback: o Billing recebe uma so");
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial - AmbienteDaSagaInfra.QuantidadeDaPeca, "a reserva aconteceu uma vez");
        var prazos = await ambiente.Infra.ConsultarAsync(db => db.SagaPrazos.AsNoTracking().Where(p => p.CorrelationId == ordem.OrdemId).ToListAsync());
        prazos.Should().HaveCount(2).And.ContainSingle(p => p.Tipo == OficinaMecanica.Contracts.Common.TipoPrazo.Pagamento);
        var os = await ambiente.OrdemAsync(ordem.OrdemId);
        os.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        os.HistoricoStatus.Count(h => h.Status == StatusOrdemDeServico.AguardandoPagamento).Should().Be(1);
        ambiente.Infra.Emails.Enviados.Count(e => e.Corpo.Contains("Aguardando pagamento")).Should().Be(1, "um e-mail por transicao confirmada, nao por tentativa");
        ambiente.Infra.Emails.Enviados.Count.Should().BeGreaterThan(emailsAntes);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0, "o retry resolveu");
    }

    [Fact]
    public async Task SweeperEmBackgroundComSqlReal_DeveCancelarOPagamentoQuandoORelogioPassaDoPrazo()
    {
        await using var ambiente = await AmbienteDaSagaInfra.CriarAsync(
            _infra, null, true, ("Saga:SweeperHabilitado", "true"), ("Saga:SweeperIntervaloSegundos", "30"));
        var ordem = await ambiente.CriarOrdemAsync();
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoAprovacao");
        await ambiente.AprovarAsync(ordem.OrdemId);
        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "AguardandoPagamento");

        // O BackgroundService do host acorda a cada 30 s do RELOGIO CONTROLADO: avancar 49 h dispara a varredura sem esperar.
        ambiente.Relogio.Advance(TimeSpan.FromHours(49));

        await ambiente.AguardarEstadoAsync(ordem.OrdemId, "Cancelada");
        (await ambiente.OrdemAsync(ordem.OrdemId)).Status.Should().Be(StatusOrdemDeServico.Cancelada);
        (await ambiente.EstoqueAsync(ordem.PecaId)).Should().Be(AmbienteDaSagaInfra.EstoqueInicial);
        (await ambiente.MensagensEmErroAsync()).Should().Be(0);
    }
}
