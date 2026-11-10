using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using OficinaMecanica.Application;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// A saga REAL (maquina de estados, activities, commands e handlers da Application) sobre o harness do MassTransit, o
// repositorio de saga em memoria e o EF InMemory, sem Docker. O teste faz o papel do Billing e da Execucao: publica os
// eventos que eles publicariam e le em harness.Sent os comandos que a saga enviou. O relogio e um FakeTimeProvider, entao
// os prazos e o sweeper sao controlados sem esperar.
public sealed class AmbienteDaSaga : IAsyncDisposable
{
    public static readonly DateTimeOffset Inicio = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _provedor;

    private AmbienteDaSaga(ServiceProvider provedor)
    {
        _provedor = provedor;
        Harness = provedor.GetRequiredService<ITestHarness>();

        // Os timeouts precisam estar definidos ANTES de o harness da saga existir: as listas de mensagens os capturam na
        // criacao. A inatividade fica enorme de proposito: o InactivityToken e de uso unico, e depois do primeiro silencio
        // todo Any() devolveria false na hora; as leituras sem espera usam um token ja cancelado (ver CenarioDaSaga).
        Harness.TestTimeout = TimeSpan.FromSeconds(20);
        Harness.TestInactivityTimeout = TimeSpan.FromHours(1);
        Saga = Harness.GetSagaStateMachineHarness<OrdemDeServicoStateMachine, OrdemDeServicoSagaState>();
        Relogio = provedor.GetRequiredService<FakeTimeProvider>();
        Logs = provedor.GetRequiredService<LoggerCapturador>();
        Emails = provedor.GetRequiredService<RegistroDeEmails>();
    }

    public IServiceProvider Servicos => _provedor;
    public ITestHarness Harness { get; }
    public ISagaStateMachineTestHarness<OrdemDeServicoStateMachine, OrdemDeServicoSagaState> Saga { get; }
    public FakeTimeProvider Relogio { get; }
    public LoggerCapturador Logs { get; }
    public RegistroDeEmails Emails { get; }

    public static async Task<AmbienteDaSaga> CriarAsync(
        Action<IServiceCollection>? ajustarServicos = null,
        params (string Chave, string? Valor)[] configuracao)
    {
        var valores = new List<(string, string?)>
        {
            ("Saga:SweeperHabilitado", "false"),
            ("Saga:PrazoAprovacaoHoras", "72"),
            ("Saga:PrazoPagamentoHoras", "48"),

            // Retry curto: uma mensagem que falha esgota as tentativas em milissegundos (os padroes de producao levam ~100 s).
            // Os testes que dependem do retry (evento antes da saga) passam os intervalos deles, que valem por indice.
            ("Mensageria:RetryIntervalosMs:0", "20"),
            ("Mensageria:RetryIntervalosMs:1", "20")
        };
        valores.AddRange(configuracao);
        var cfg = ConfiguracaoDeTeste.Criar(valores.ToArray());

        var logs = new LoggerCapturador();
        var relogio = new FakeTimeProvider(Inicio);
        var banco = "saga_" + Guid.NewGuid();

        var services = new ServiceCollection();
        services.AddLogging(l =>
        {
            l.ClearProviders();
            l.SetMinimumLevel(LogLevel.Warning);
            l.AddProvider(logs);
        });
        services.AddSingleton(logs);
        services.AddSingleton<IHostEnvironment>(new AmbienteFalso("Testing"));
        services.AddApplication();
        services.AddInfrastructure(cfg);

        // Banco em memoria no lugar do SQL Server (mesma troca do CustomWebApplicationFactory).
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(banco));

        var emails = new RegistroDeEmails();
        services.RemoveAll<IEmailService>();
        services.AddSingleton<IEmailService>(emails);
        services.AddSingleton(emails);

        services.RemoveAll<TimeProvider>();
        services.AddSingleton(relogio);
        services.AddSingleton<TimeProvider>(relogio);

        ajustarServicos?.Invoke(services);

        services.AddServicosDaSaga(cfg);

        // AddMensageria traz o que a saga usa alem do bus (opcoes, publisher). O harness SUBSTITUI o bus e o repositorio de saga
        // do AddMensageria (documentado pelo MassTransit): por isso a saga e o endpoint os-saga, com o mesmo retry da API,
        // sao declarados de novo aqui, no bloco do proprio harness, e o repositorio da saga fica em memoria.
        services.AddMensageria(cfg);
        services.AddMassTransitTestHarness(bus =>
        {
            bus.AddSagaDaOrdemDeServico(repositorioEmMemoria: true);
            bus.UsingInMemory((contexto, endpoints) => endpoints.ReceiveEndpointDaSaga(contexto));
        });

        var provedor = services.BuildServiceProvider(validateScopes: true);
        var ambiente = new AmbienteDaSaga(provedor);
        await ambiente.Harness.Start();

        return ambiente;
    }

    public async Task<T> NoEscopoAsync<T>(Func<IServiceProvider, Task<T>> acao)
    {
        await using var escopo = _provedor.CreateAsyncScope();
        return await acao(escopo.ServiceProvider);
    }

    public Task<T> ConsultarAsync<T>(Func<AppDbContext, Task<T>> consulta) =>
        NoEscopoAsync(sp => consulta(sp.GetRequiredService<AppDbContext>()));

    public Task<T> EnviarAsync<T>(IRequest<T> comando) =>
        NoEscopoAsync(sp => sp.GetRequiredService<IMediator>().Send(comando));

    public async ValueTask DisposeAsync()
    {
        await Harness.Stop();
        await _provedor.DisposeAsync();
    }
}
