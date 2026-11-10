using System.Collections.Concurrent;
using MassTransit;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Application.Commands.MarcarDiagnosticoIniciado;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// Mensagem so dos testes: leva o MessageId no mesmo formato dos contratos reais.
public sealed record MensagemDeTeste(Guid MessageId, Guid OrdemDeServicoId);

public sealed class FalhaTecnicaSimuladaException : Exception
{
    public FalhaTecnicaSimuladaException() : base("Falha técnica simulada pelo teste.")
    {
    }
}

public sealed record ConsumoRegistrado(
    Guid MessageIdDoEnvelope,
    Guid MessageIdDoContrato,
    Guid OrdemDeServicoId,
    int Tentativa,
    Type EstrategiaDeExecucao,
    bool TransacaoAberta);

// Singleton compartilhado entre os consumers de teste e o teste: o que cada consumer viu em cada tentativa.
// A estrategia de execucao e registrada pelo TIPO, e nao por RetriesOnFailure: dentro do strategy.ExecuteAsync que
// o MassTransit abre em volta do consumer o EF suspende o retry e RetriesOnFailure da false mesmo com o retry configurado.
public sealed class RegistroDeConsumo
{
    private readonly ConcurrentQueue<ConsumoRegistrado> _consumos = new();
    private readonly ConcurrentQueue<Guid> _prazosRecebidos = new();
    private int _tentativas;
    private int _conexaoDerrubada;

    public IReadOnlyList<ConsumoRegistrado> Consumos => _consumos.ToArray();

    public IReadOnlyList<Guid> PrazosRecebidos => _prazosRecebidos.ToArray();

    // Quantas tentativas, a partir da primeira, falham DEPOIS de o consumer ter escrito no banco.
    public int TentativasQueFalhamDepoisDeEscrever { get; set; }

    // Excecao que o ConsumerQueLanca lanca em toda tentativa.
    public Func<Exception>? FabricaDeExcecao { get; set; }

    // Faz o ConsumerDeDiagnostico matar a propria conexao com o banco uma unica vez, depois de escrever.
    public bool DerrubarConexaoUmaVez { get; set; }

    public int NovaTentativa() => Interlocked.Increment(ref _tentativas);

    public bool PodeDerrubarConexao() => DerrubarConexaoUmaVez && Interlocked.Exchange(ref _conexaoDerrubada, 1) == 0;

    public void Registrar(ConsumoRegistrado consumo) => _consumos.Enqueue(consumo);

    public void RegistrarPrazo(Guid messageId) => _prazosRecebidos.Enqueue(messageId);

    public int Observar(AppDbContext db, Guid? messageIdDoEnvelope, Guid messageIdDoContrato, Guid ordemDeServicoId)
    {
        var tentativa = NovaTentativa();
        Registrar(new ConsumoRegistrado(
            messageIdDoEnvelope ?? Guid.Empty,
            messageIdDoContrato,
            ordemDeServicoId,
            tentativa,
            db.Database.CreateExecutionStrategy().GetType(),
            db.Database.CurrentTransaction != null));
        return tentativa;
    }
}

public sealed class ConsumerDeOrdemAberta : IConsumer<OrdemDeServicoAberta>
{
    private readonly RegistroDeConsumo _registro;
    private readonly AppDbContext _db;

    public ConsumerDeOrdemAberta(RegistroDeConsumo registro, AppDbContext db)
    {
        _registro = registro;
        _db = db;
    }

    public Task Consume(ConsumeContext<OrdemDeServicoAberta> context)
    {
        _registro.Observar(_db, context.MessageId, context.Message.MessageId, context.Message.OrdemDeServicoId);
        return Task.CompletedTask;
    }
}

public sealed class ConsumerDeMensagemDeTeste : IConsumer<MensagemDeTeste>
{
    private readonly RegistroDeConsumo _registro;
    private readonly AppDbContext _db;

    public ConsumerDeMensagemDeTeste(RegistroDeConsumo registro, AppDbContext db)
    {
        _registro = registro;
        _db = db;
    }

    public Task Consume(ConsumeContext<MensagemDeTeste> context)
    {
        _registro.Observar(_db, context.MessageId, context.Message.MessageId, context.Message.OrdemDeServicoId);
        return Task.CompletedTask;
    }
}

// Faz o papel da activity da saga: dispara um command interno no MESMO escopo (mesmo AppDbContext e mesma
// transacao do MassTransit) e, se configurado, falha depois de o command ter escrito.
public sealed class ConsumerDeDiagnostico : IConsumer<MensagemDeTeste>
{
    private readonly RegistroDeConsumo _registro;
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;

    public ConsumerDeDiagnostico(RegistroDeConsumo registro, IMediator mediator, AppDbContext db)
    {
        _registro = registro;
        _mediator = mediator;
        _db = db;
    }

    public async Task Consume(ConsumeContext<MensagemDeTeste> context)
    {
        var tentativa = _registro.Observar(_db, context.MessageId, context.Message.MessageId, context.Message.OrdemDeServicoId);

        await _mediator.Send(new MarcarDiagnosticoIniciadoCommand(context.Message.OrdemDeServicoId), context.CancellationToken);

        if (_registro.PodeDerrubarConexao())
            await ConexaoDoBanco.DerrubarAsync(_db);

        if (tentativa <= _registro.TentativasQueFalhamDepoisDeEscrever)
            throw new FalhaTecnicaSimuladaException();
    }
}

// Lanca a excecao que o teste escolheu em toda tentativa, para provar o que o UseMessageRetry repete e o que nao.
public sealed class ConsumerQueLanca : IConsumer<MensagemDeTeste>
{
    private readonly RegistroDeConsumo _registro;
    private readonly AppDbContext _db;

    public ConsumerQueLanca(RegistroDeConsumo registro, AppDbContext db)
    {
        _registro = registro;
        _db = db;
    }

    public Task Consume(ConsumeContext<MensagemDeTeste> context)
    {
        _registro.Observar(_db, context.MessageId, context.Message.MessageId, context.Message.OrdemDeServicoId);
        throw _registro.FabricaDeExcecao!();
    }
}

// Publica um evento pelo ConsumeContext (que vai para o outbox do consumer) e, se configurado, falha depois.
public sealed class ConsumerQuePublica : IConsumer<MensagemDeTeste>
{
    private readonly RegistroDeConsumo _registro;
    private readonly AppDbContext _db;

    public ConsumerQuePublica(RegistroDeConsumo registro, AppDbContext db)
    {
        _registro = registro;
        _db = db;
    }

    public async Task Consume(ConsumeContext<MensagemDeTeste> context)
    {
        var tentativa = _registro.Observar(_db, context.MessageId, context.Message.MessageId, context.Message.OrdemDeServicoId);

        await context.Publish(new PrazoExpiradoBuilder().DaOrdem(context.Message.OrdemDeServicoId).Build(), context.CancellationToken);

        if (_registro.PodeDerrubarConexao())
            await ConexaoDoBanco.DerrubarAsync(_db);

        if (tentativa <= _registro.TentativasQueFalhamDepoisDeEscrever)
            throw new FalhaTecnicaSimuladaException();
    }
}

public sealed class ConsumerDePrazoExpirado : IConsumer<PrazoExpirado>
{
    private readonly RegistroDeConsumo _registro;

    public ConsumerDePrazoExpirado(RegistroDeConsumo registro) => _registro = registro;

    public Task Consume(ConsumeContext<PrazoExpirado> context)
    {
        _registro.RegistrarPrazo(context.Message.MessageId);
        return Task.CompletedTask;
    }
}

// Handler de domain event que ESCREVE no banco: o despacho pos-commit so funciona para ele em escopo DI novo (no
// escopo do consumer o AppDbContext ainda esta com a transacao do MassTransit aberta).
public sealed class GravadorDeServicoNoPosCommitHandler : MediatR.INotificationHandler<OficinaMecanica.Domain.Events.StatusOrdemAlteradoEvent>
{
    public const string PrefixoDoNome = "gravado-no-pos-commit-";

    private readonly AppDbContext _db;

    public GravadorDeServicoNoPosCommitHandler(AppDbContext db) => _db = db;

    public async Task Handle(OficinaMecanica.Domain.Events.StatusOrdemAlteradoEvent notification, CancellationToken cancellationToken)
    {
        _db.Add(new ServicoBuilder().ComNome(PrefixoDoNome + notification.Numero).Build());
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public static class ConexaoDoBanco
{
    // O SQL Server mata a sessao do consumer e o commit do MassTransit falha; quem recupera e o UseMessageRetry, numa
    // tentativa com escopo, conexao e transacao novos.
    public static async Task DerrubarAsync(AppDbContext db)
    {
        var spid = (await db.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS [Value]").ToListAsync()).Single();

        await using var outraConexao = new SqlConnection(db.Database.GetConnectionString());
        await outraConexao.OpenAsync();
        await using var comando = outraConexao.CreateCommand();
        comando.CommandText = $"KILL {spid}";
        await comando.ExecuteNonQueryAsync();
    }
}
