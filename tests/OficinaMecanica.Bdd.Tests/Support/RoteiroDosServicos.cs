using System.Collections.Concurrent;
using MassTransit;
using OficinaMecanica.Contracts.Commands;

namespace OficinaMecanica.Bdd.Tests.Support;

// O Billing e a Execucao deste teste nao tem regra de negocio: sao consumers nas filas reais (billing-comandos e
// execucao-comandos) que guardam o comando recebido e, se o cenario programou, respondem com o evento que o cenario manda
// (ou lancam uma falha tecnica). Sem programacao ficam em silencio, e quem fala por eles e o passo do cenario. A logica dos
// servicos de verdade nao e reimplementada aqui: ela e coberta pelos testes de cada um.
public sealed class RoteiroDosServicos
{
    private readonly ConcurrentDictionary<Type, Func<object, object?>> _reacoes = new();
    private readonly ConcurrentDictionary<Type, Func<object, Exception>> _falhas = new();
    private readonly ConcurrentQueue<ComandoRecebido> _recebidos = new();

    // O que o servico responde ao receber o comando T; a resposta e publicada como evento.
    public void AoReceber<TComando>(Func<TComando, object?> resposta) where TComando : class
    {
        _falhas.TryRemove(typeof(TComando), out _);
        _reacoes[typeof(TComando)] = comando => resposta((TComando)comando);
    }

    // Falha tecnica no consumer: o MassTransit publica Fault<TComando>, que e o que a saga trata como compensacao presa.
    public void FalharAoReceber<TComando>(string erro) where TComando : class
    {
        _reacoes.TryRemove(typeof(TComando), out _);
        _falhas[typeof(TComando)] = _ => new InvalidOperationException(erro);
    }

    public IReadOnlyList<ComandoRecebido> Recebidos => _recebidos.ToArray();

    public IReadOnlyList<TComando> RecebidosDoTipo<TComando>() where TComando : class =>
        _recebidos.Select(r => r.Comando).OfType<TComando>().ToList();

    internal async Task ProcessarAsync<TComando>(string servico, ConsumeContext<TComando> contexto) where TComando : class
    {
        _recebidos.Enqueue(new ComandoRecebido(servico, contexto.Message));

        if (_falhas.TryGetValue(typeof(TComando), out var criarFalha))
            throw criarFalha(contexto.Message);

        if (_reacoes.TryGetValue(typeof(TComando), out var reacao) && reacao(contexto.Message) is { } resposta)
            await contexto.Publish(resposta, resposta.GetType());
    }
}

public sealed record ComandoRecebido(string Servico, object Comando)
{
    public string Nome => Comando.GetType().Name;
}

public sealed class BillingSimulado :
    IConsumer<GerarOrcamento>,
    IConsumer<SolicitarPagamento>,
    IConsumer<CancelarPagamento>,
    IConsumer<CancelarOrcamento>,
    IConsumer<EstornarPagamento>
{
    public const string Nome = "Billing";

    private readonly RoteiroDosServicos _roteiro;

    public BillingSimulado(RoteiroDosServicos roteiro)
    {
        _roteiro = roteiro;
    }

    public Task Consume(ConsumeContext<GerarOrcamento> context) => _roteiro.ProcessarAsync(Nome, context);
    public Task Consume(ConsumeContext<SolicitarPagamento> context) => _roteiro.ProcessarAsync(Nome, context);
    public Task Consume(ConsumeContext<CancelarPagamento> context) => _roteiro.ProcessarAsync(Nome, context);
    public Task Consume(ConsumeContext<CancelarOrcamento> context) => _roteiro.ProcessarAsync(Nome, context);
    public Task Consume(ConsumeContext<EstornarPagamento> context) => _roteiro.ProcessarAsync(Nome, context);
}

public sealed class ExecucaoSimulada :
    IConsumer<SolicitarDiagnostico>,
    IConsumer<IniciarExecucao>,
    IConsumer<CancelarProducao>
{
    public const string Nome = "Execução";

    private readonly RoteiroDosServicos _roteiro;

    public ExecucaoSimulada(RoteiroDosServicos roteiro)
    {
        _roteiro = roteiro;
    }

    public Task Consume(ConsumeContext<SolicitarDiagnostico> context) => _roteiro.ProcessarAsync(Nome, context);
    public Task Consume(ConsumeContext<IniciarExecucao> context) => _roteiro.ProcessarAsync(Nome, context);
    public Task Consume(ConsumeContext<CancelarProducao> context) => _roteiro.ProcessarAsync(Nome, context);
}
