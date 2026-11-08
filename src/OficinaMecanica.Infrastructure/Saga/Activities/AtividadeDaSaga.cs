using MassTransit;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Base das activities da saga: so a mecanica do MassTransit (visitor, probe, repasse ao proximo passo e a falha) e o
// carimbo de AtualizadaEm. Cada activity concreta tem um unico motivo para existir, que e o que o nome diz.
//
// Uma excecao dentro de ExecutarAsync sobe sem ser tratada de proposito: a transacao do consumer volta inteira (OS,
// estoque, saga e mensagens do outbox) e o retry do endpoint reprocessa a mensagem do zero. Nao existe rollback manual.
public abstract class AtividadeDaSaga : IStateMachineActivity<OrdemDeServicoSagaState>
{
    private readonly TimeProvider _relogio;

    protected AtividadeDaSaga(TimeProvider relogio)
    {
        _relogio = relogio;
    }

    protected DateTimeOffset Agora => _relogio.GetUtcNow();

    // Passo que so observa (o log de uma falha tecnica) nao e atualizacao da saga: carimbar AtualizadaEm nele esconderia
    // uma saga parada, que e justamente o que esse campo ajuda a achar.
    protected virtual bool CarimbaAtualizacao => true;

    protected abstract Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState> contexto);

    public void Probe(ProbeContext context) => context.CreateScope(GetType().Name);

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    // Exigida por IStateMachineActivity<TSaga>: o MassTransit so a chama para comportamento sem dado de evento (ex.: ao entrar
    // num estado), que esta maquina nao declara. Mantenho o mesmo caminho da versao com evento para as duas nao divergirem.
    public Task Execute(BehaviorContext<OrdemDeServicoSagaState> context, IBehavior<OrdemDeServicoSagaState> next) =>
        ExecutarEDepoisAsync(context, () => next.Execute(context));

    public Task Execute<T>(BehaviorContext<OrdemDeServicoSagaState, T> context, IBehavior<OrdemDeServicoSagaState, T> next)
        where T : class => ExecutarEDepoisAsync(context, () => next.Execute(context));

    private async Task ExecutarEDepoisAsync(BehaviorContext<OrdemDeServicoSagaState> context, Func<Task> proximoPasso)
    {
        if (CarimbaAtualizacao)
            context.Saga.AtualizadaEm = Agora;

        await ExecutarAsync(context);
        await proximoPasso();
    }

    public Task Faulted<TException>(BehaviorExceptionContext<OrdemDeServicoSagaState, TException> context, IBehavior<OrdemDeServicoSagaState> next)
        where TException : Exception => next.Faulted(context);

    public Task Faulted<T, TException>(BehaviorExceptionContext<OrdemDeServicoSagaState, T, TException> context, IBehavior<OrdemDeServicoSagaState, T> next)
        where T : class where TException : Exception => next.Faulted(context);
}

// Variante para a activity que precisa dos dados do evento que a disparou.
public abstract class AtividadeDaSaga<TMensagem> : IStateMachineActivity<OrdemDeServicoSagaState, TMensagem>
    where TMensagem : class
{
    private readonly TimeProvider _relogio;

    protected AtividadeDaSaga(TimeProvider relogio)
    {
        _relogio = relogio;
    }

    protected DateTimeOffset Agora => _relogio.GetUtcNow();

    // Passo que so observa (o log de uma falha tecnica) nao e atualizacao da saga: carimbar AtualizadaEm nele esconderia
    // uma saga parada, que e justamente o que esse campo ajuda a achar.
    protected virtual bool CarimbaAtualizacao => true;

    protected abstract Task ExecutarAsync(BehaviorContext<OrdemDeServicoSagaState, TMensagem> contexto);

    public void Probe(ProbeContext context) => context.CreateScope(GetType().Name);

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<OrdemDeServicoSagaState, TMensagem> context, IBehavior<OrdemDeServicoSagaState, TMensagem> next)
    {
        if (CarimbaAtualizacao)
            context.Saga.AtualizadaEm = Agora;
        await ExecutarAsync(context);
        await next.Execute(context);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<OrdemDeServicoSagaState, TMensagem, TException> context, IBehavior<OrdemDeServicoSagaState, TMensagem> next)
        where TException : Exception => next.Faulted(context);
}
