using System.Collections.Concurrent;
using MassTransit;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// Estado compartilhado entre o teste e os consumers que fazem o papel do Billing e da Execucao: o que cada um recebeu e como
// deve responder. Nao e um mock da saga: sao consumers de verdade, ligados as filas de verdade (Filas.Billing e Filas.Execucao),
// que respondem com os contratos reais.
public sealed class ComportamentoDosServicos
{
    private readonly ConcurrentQueue<object> _billing = new();
    private readonly ConcurrentQueue<object> _execucao = new();

    // A Execucao diagnostica sozinha (publica DiagnosticoIniciado e DiagnosticoConcluido ao receber SolicitarDiagnostico).
    public volatile bool DiagnosticoAutomatico = true;

    // O Billing gera o orcamento e o link de pagamento sozinho.
    public volatile bool BillingResponde = true;

    // O Billing responde EstornoFalhou em vez de PagamentoEstornado.
    public volatile bool EstornoFalha;

    // Quando preenchida, o Billing segura o GerarOrcamento ate a porta abrir (cancelamento antes de criar).
    public TaskCompletionSource? PortaDoGerarOrcamento { get; set; }

    // OS cujo orcamento foi cancelado ANTES do GerarOrcamento: o Billing real registra um tombstone e responde OrcamentoNaoGerado.
    public ConcurrentDictionary<Guid, bool> CancelamentoPrevioDoOrcamento { get; } = new();

    // Comandos cujo consumer lanca uma excecao tecnica (nao um evento de negocio): o MassTransit move a mensagem para a fila
    // _error do destino e publica o Fault<T> que a saga recebe, o caminho real de uma falha do Billing ou da Execucao.
    private readonly ConcurrentDictionary<Type, string> _falhasTecnicas = new();

    public IReadOnlyList<object> RecebidoPeloBilling => _billing.ToArray();
    public IReadOnlyList<object> RecebidoPelaExecucao => _execucao.ToArray();

    public void RegistrarNoBilling(object comando) => _billing.Enqueue(comando);
    public void RegistrarNaExecucao(object comando) => _execucao.Enqueue(comando);

    public void LancarExcecaoTecnicaEm<T>(string mensagem) where T : class => _falhasTecnicas[typeof(T)] = mensagem;
    public void PararDeLancarExcecaoTecnicaEm<T>() where T : class => _falhasTecnicas.TryRemove(typeof(T), out _);

    // Chamado pelo consumer logo depois de registrar o comando: a tentativa que falha tambem conta como recebida.
    public void LancarSeConfigurado<T>() where T : class
    {
        if (_falhasTecnicas.TryGetValue(typeof(T), out var mensagem))
            throw new InvalidOperationException(mensagem);
    }

    public int NoBilling<T>(Guid ordemDeServicoId) where T : class =>
        RecebidoPeloBilling.OfType<T>().Count(c => OrdemDe(c) == ordemDeServicoId);

    public int NaExecucao<T>(Guid ordemDeServicoId) where T : class =>
        RecebidoPelaExecucao.OfType<T>().Count(c => OrdemDe(c) == ordemDeServicoId);

    public static Guid OrdemDe(object comando) =>
        (Guid)(comando.GetType().GetProperty("OrdemDeServicoId")?.GetValue(comando) ?? Guid.Empty);

    // Ids que o "Billing" devolve: derivados da OS, como o Billing real faz com os dele.
    public static Guid OrcamentoDa(Guid ordemDeServicoId) => IdentificadorDeMensagem.Para(ordemDeServicoId, "orcamento-do-billing");
    public static Guid PagamentoDa(Guid ordemDeServicoId) => IdentificadorDeMensagem.Para(ordemDeServicoId, "pagamento-do-billing");
}

public sealed class BillingSimulado :
    IConsumer<GerarOrcamento>, IConsumer<SolicitarPagamento>, IConsumer<CancelarOrcamento>, IConsumer<CancelarPagamento>, IConsumer<EstornarPagamento>
{
    private readonly ComportamentoDosServicos _comportamento;

    public BillingSimulado(ComportamentoDosServicos comportamento) => _comportamento = comportamento;

    private static Guid Id(Guid os, string resposta) => IdentificadorDeMensagem.Para(os, resposta);

    public async Task Consume(ConsumeContext<GerarOrcamento> context)
    {
        var comando = context.Message;
        _comportamento.RegistrarNoBilling(comando);

        if (_comportamento.PortaDoGerarOrcamento is { } porta)
            await porta.Task;

        if (!_comportamento.BillingResponde)
            return;

        if (_comportamento.CancelamentoPrevioDoOrcamento.ContainsKey(comando.OrdemDeServicoId))
        {
            await context.Publish(new OrcamentoNaoGeradoBuilder()
                .ComMessageId(Id(comando.OrdemDeServicoId, nameof(OrcamentoNaoGerado))).DaOrdem(comando.OrdemDeServicoId)
                .ComMotivo("Cancelamento prévio do orçamento.").Build());
            return;
        }

        await context.Publish(new OrcamentoGeradoBuilder()
            .ComMessageId(Id(comando.OrdemDeServicoId, nameof(OrcamentoGerado))).DaOrdem(comando.OrdemDeServicoId)
            .ComOrcamento(ComportamentoDosServicos.OrcamentoDa(comando.OrdemDeServicoId)).ComValorTotal(comando.ValorTotal)
            .ValidoAte(comando.ValidoAte).Build());
    }

    public async Task Consume(ConsumeContext<SolicitarPagamento> context)
    {
        var comando = context.Message;
        _comportamento.RegistrarNoBilling(comando);
        _comportamento.LancarSeConfigurado<SolicitarPagamento>();
        if (!_comportamento.BillingResponde)
            return;

        await context.Publish(new PagamentoSolicitadoBuilder()
            .ComMessageId(Id(comando.OrdemDeServicoId, nameof(PagamentoSolicitado))).DaOrdem(comando.OrdemDeServicoId)
            .ComPagamento(ComportamentoDosServicos.PagamentoDa(comando.OrdemDeServicoId))
            .ComLink($"https://pagamento.local/checkout/{comando.OrdemDeServicoId:N}").ExpiraEm(comando.ExpiraEm).Build());
    }

    public Task Consume(ConsumeContext<CancelarOrcamento> context)
    {
        _comportamento.RegistrarNoBilling(context.Message);
        _comportamento.CancelamentoPrevioDoOrcamento[context.Message.OrdemDeServicoId] = true;
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<CancelarPagamento> context)
    {
        var comando = context.Message;
        _comportamento.RegistrarNoBilling(comando);
        _comportamento.LancarSeConfigurado<CancelarPagamento>();
        if (!_comportamento.BillingResponde)
            return;

        var cancelado = new PagamentoCanceladoBuilder().ComMessageId(Id(comando.OrdemDeServicoId, nameof(PagamentoCancelado))).DaOrdem(comando.OrdemDeServicoId);
        await context.Publish((comando.PagamentoId is { } pagamentoId ? cancelado.ComPagamento(pagamentoId) : cancelado.SemPagamento()).Build());
    }

    public async Task Consume(ConsumeContext<EstornarPagamento> context)
    {
        var comando = context.Message;
        _comportamento.RegistrarNoBilling(comando);

        if (_comportamento.EstornoFalha)
        {
            await context.Publish(new EstornoFalhouBuilder()
                .ComMessageId(Id(comando.OrdemDeServicoId, nameof(EstornoFalhou))).DaOrdem(comando.OrdemDeServicoId)
                .ComPagamento(comando.PagamentoId).ComErro("Gateway indisponível.").Build());
            return;
        }

        await context.Publish(new PagamentoEstornadoBuilder()
            .ComMessageId(Id(comando.OrdemDeServicoId, nameof(PagamentoEstornado))).DaOrdem(comando.OrdemDeServicoId)
            .ComPagamento(comando.PagamentoId).Build());
    }
}

public sealed class ExecucaoSimulada : IConsumer<SolicitarDiagnostico>, IConsumer<IniciarExecucao>, IConsumer<CancelarProducao>
{
    private readonly ComportamentoDosServicos _comportamento;

    public ExecucaoSimulada(ComportamentoDosServicos comportamento) => _comportamento = comportamento;

    public async Task Consume(ConsumeContext<SolicitarDiagnostico> context)
    {
        var comando = context.Message;
        _comportamento.RegistrarNaExecucao(comando);
        if (!_comportamento.DiagnosticoAutomatico)
            return;

        await context.Publish(new DiagnosticoIniciadoBuilder()
            .ComMessageId(IdentificadorDeMensagem.Para(comando.OrdemDeServicoId, nameof(DiagnosticoIniciado))).DaOrdem(comando.OrdemDeServicoId).Build());
        await context.Publish(new DiagnosticoConcluidoBuilder()
            .ComMessageId(IdentificadorDeMensagem.Para(comando.OrdemDeServicoId, nameof(DiagnosticoConcluido))).DaOrdem(comando.OrdemDeServicoId)
            .ComLaudo("Pastilhas gastas.").Build());
    }

    public Task Consume(ConsumeContext<IniciarExecucao> context)
    {
        _comportamento.RegistrarNaExecucao(context.Message);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<CancelarProducao> context)
    {
        _comportamento.RegistrarNaExecucao(context.Message);
        return Task.CompletedTask;
    }
}
