using FluentValidation;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Infrastructure.Messaging;

public static class MensageriaExtensions
{
    // Registra o bus do MassTransit, o outbox/inbox transacional sobre o AppDbContext e a porta de publicacao.
    //
    // Os dois ultimos parametros sao ganchos de composicao para os testes: eles acrescentam consumers e endpoints
    // de teste ao MESMO bus configurado aqui (mesmo outbox, mesmo retry), em vez de montar um bus paralelo. Em
    // producao ninguem os passa; a saga entra direto neste metodo.
    public static IServiceCollection AddMensageria(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? registrarConsumers = null,
        Action<IBusRegistrationContext, IReceiveConfigurator>? configurarEndpoints = null)
    {
        services.AddOptions<OpcoesDaMensageria>().Bind(configuration.GetSection(OpcoesDaMensageria.Secao)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OpcoesDaMensageria>, ValidadorDasOpcoesDaMensageria>());
        services.AddOptions<OpcoesDoRabbitMq>().Bind(configuration.GetSection(OpcoesDoRabbitMq.Secao));

        var opcoes = LerOpcoes(configuration);

        // Sem WaitUntilStarted, StartAsync devolve antes de os endpoints e bindings existirem e uma mensagem
        // publicada logo em seguida pode se perder.
        services.Configure<MassTransitHostOptions>(host =>
        {
            host.WaitUntilStarted = true;
            host.StartTimeout = TimeSpan.FromSeconds(opcoes.TimeoutDeInicioSegundos);
        });

        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        var usaRabbitMq = opcoes.UsaRabbitMq;
        var usaOutboxDoEf = opcoes.UsaOutboxDoEntityFramework;

        if (usaRabbitMq)
            services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });

        // O check do bus que o MassTransit registra tambem nao pode tirar o pod de rotacao por causa do broker, mesmo
        // que um dia ele passe a reportar a falta de conexao: rebaixo a falha dele para Degraded.
        services.PostConfigure<HealthCheckServiceOptions>(health =>
        {
            foreach (var registro in health.Registrations.Where(r => r.Name == "masstransit-bus"))
                registro.FailureStatus = HealthStatus.Degraded;
        });

        services.AddMassTransit(x =>
        {
            registrarConsumers?.Invoke(x);

            if (usaOutboxDoEf)
            {
                // Nao defino o IsolationLevel do outbox: o padrao do MassTransit (RepeatableRead) funciona com o
                // servico de entrega do bus outbox, que le com READPAST. Serializable o quebra (erro 650 do SQL
                // Server) e, sem o bus outbox, gera deadlock sob carga.
                x.AddEntityFrameworkOutbox<AppDbContext>(outbox =>
                {
                    outbox.UseSqlServer();
                    outbox.UseBusOutbox();
                    outbox.QueryDelay = TimeSpan.FromMilliseconds(opcoes.OutboxQueryDelayMs);
                });
            }

            if (usaRabbitMq)
            {
                x.UsingRabbitMq((contexto, cfg) =>
                {
                    var rabbit = contexto.GetRequiredService<IOptions<OpcoesDoRabbitMq>>().Value;
                    cfg.Host(rabbit.Host, rabbit.Port, rabbit.VirtualHost, host =>
                    {
                        if (!string.IsNullOrEmpty(rabbit.Username)) host.Username(rabbit.Username);
                        if (!string.IsNullOrEmpty(rabbit.Password)) host.Password(rabbit.Password);
                    });

                    configurarEndpoints?.Invoke(contexto, cfg);
                });
            }
            else
            {
                x.UsingInMemory((contexto, cfg) => configurarEndpoints?.Invoke(contexto, cfg));
            }
        });

        return services;
    }

    // O binder lanca para texto que nao e nome de valor do enum; a mensagem dele e em ingles e nao diz que a causa e
    // a secao Mensageria.
    private static OpcoesDaMensageria LerOpcoes(IConfiguration configuration)
    {
        try
        {
            return configuration.GetSection(OpcoesDaMensageria.Secao).Get<OpcoesDaMensageria>() ?? new OpcoesDaMensageria();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"A seção {OpcoesDaMensageria.Secao} da configuração tem um valor inválido. {ex.Message}", ex);
        }
    }

    // Monta os filtros que todo endpoint deste servico precisa: retry por fora, outbox/inbox por dentro. Com o retry
    // por fora, cada tentativa reprocessa a mensagem inteira em uma transacao nova do outbox. O MassTransit 8.5.12 ja
    // posiciona o retry assim mesmo se as chamadas viessem em outra ordem (o probe do bus mostra o pipeline); mantenho
    // a ordem explicita para nao depender disso.
    public static void ConfigurarRetryEOutbox(this IReceiveEndpointConfigurator endpoint, IBusRegistrationContext contexto)
    {
        var opcoes = contexto.GetRequiredService<IOptions<OpcoesDaMensageria>>().Value;

        endpoint.UseMessageRetry(retry =>
        {
            retry.Intervals(opcoes.IntervalosDeRetry());

            // Regra de negocio violada, conflito de estado e validacao reprovada nao mudam de resultado se a
            // mensagem for repetida; so o erro tecnico (banco, rede) merece nova tentativa.
            retry.Ignore<ConflitoException>();
            retry.Ignore<DomainException>();
            retry.Ignore<ValidationException>();
        });

        if (opcoes.UsaOutboxDoEntityFramework)
            endpoint.UseEntityFrameworkOutbox<AppDbContext>(contexto);
    }
}
