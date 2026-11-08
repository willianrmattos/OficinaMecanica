using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OficinaMecanica.Contracts;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Saga.Activities;

namespace OficinaMecanica.Infrastructure.Saga;

// Composicao da saga. Ela entra no bus pelos ganchos de AddMensageria (registrarConsumers e configurarEndpoints), entao
// herda o mesmo bus, o mesmo outbox/inbox e o mesmo retry, e nao ha segundo bus nem segunda conexao com o broker. Os testes
// que precisam de mais consumers usam as tres pecas abaixo direto; a API usa AddMensageriaComSaga.
public static class SagaExtensions
{
    // Variaveis Saga__*, o repositorio de prazos, o sweeper e as activities.
    public static IServiceCollection AddServicosDaSaga(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SagaOptions>().Bind(configuration.GetSection(SagaOptions.Secao)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SagaOptions>, ValidadorDasOpcoesDaSaga>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPrazoRepository, PrazoRepository>();
        services.AddScoped<SweeperDePrazos>();
        services.AddHostedService<SweeperDePrazosBackgroundService>();

        // Escopo do consumer: a activity compartilha o AppDbContext (e a transacao) da mensagem que a dispara.
        var activities = typeof(AtividadeDaSaga).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IStateMachineActivity).IsAssignableFrom(t));
        foreach (var activity in activities)
            services.AddScoped(activity);

        return services;
    }

    // Dois eventos da mesma OS se serializam no banco porque o repositorio EF do MassTransit le a linha da saga com UPDLOCK e
    // ROWLOCK dentro da transacao do consumer. Esse bloqueio e o comportamento padrao dele (modo Pessimistic e comando do SQL
    // Server): declaro os dois ajustes abaixo so para a intencao ficar explicita, e quem garante o bloqueio e o teste que
    // captura o SQL (SagaConcorrenciaTests), que reprova o modo Optimistic. A linha da saga nao tem RowVersion, entao o modo
    // otimista nao enxergaria o conflito. O isolamento fica no padrao do MassTransit: Serializable quebraria o servico de
    // entrega do outbox.
    //
    // repositorioEmMemoria so existe para os testes sem SQL (Mensageria:Persistencia = Nenhuma): fora de Development e
    // Testing o ValidadorDasOpcoesDaMensageria nao deixa a API subir com essa persistencia.
    public static void AddSagaDaOrdemDeServico(this IBusRegistrationConfigurator bus, bool repositorioEmMemoria = false)
    {
        var saga = bus.AddSagaStateMachine<OrdemDeServicoStateMachine, OrdemDeServicoSagaState>();

        if (repositorioEmMemoria)
        {
            saga.InMemoryRepository();
            return;
        }

        saga.EntityFrameworkRepository(repositorio =>
        {
            repositorio.ConcurrencyMode = ConcurrencyMode.Pessimistic;
            repositorio.ExistingDbContext<AppDbContext>();
            repositorio.UseSqlServer();
        });
    }

    // A fila da saga nasce quando o bus sobe (WaitUntilStarted), junto com o binding de cada evento que ela consome. O
    // exchange de OrdemDeServicoAberta so e criado no primeiro publish e, sem fila ligada a ele, o RabbitMQ descarta a
    // mensagem: com a fila ja ligada, nenhuma OS aberta fica sem saga.
    public static void ReceiveEndpointDaSaga(this IReceiveConfigurator bus, IBusRegistrationContext contexto) =>
        bus.ReceiveEndpoint(Filas.Saga, endpoint =>
        {
            endpoint.ConfigurarRetryEOutbox(contexto);
            endpoint.ConfigureSaga<OrdemDeServicoSagaState>(contexto);
        });

    // Composicao da API: mensageria + saga. O repositorio da saga acompanha a persistencia escolhida para a mensageria, o
    // mesmo ponto de troca dos testes (EntityFramework em producao; Nenhuma so em Development e Testing).
    public static IServiceCollection AddMensageriaComSaga(this IServiceCollection services, IConfiguration configuration)
    {
        var repositorioEmMemoria = !MensageriaExtensions.LerOpcoes(configuration).UsaOutboxDoEntityFramework;

        services.AddServicosDaSaga(configuration);
        return services.AddMensageria(
            configuration,
            registrarConsumers: bus => bus.AddSagaDaOrdemDeServico(repositorioEmMemoria),
            configurarEndpoints: (contexto, bus) => bus.ReceiveEndpointDaSaga(contexto));
    }
}
