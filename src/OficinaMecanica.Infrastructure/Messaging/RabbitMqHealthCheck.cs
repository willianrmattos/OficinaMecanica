using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OficinaMecanica.Infrastructure.Messaging;

// Abre (e fecha) uma conexao AMQP de verdade. Optei por nao confiar no check do bus do MassTransit para saber se o
// broker esta de pe: enquanto o servico nao tem receive endpoints ele so publica, e o check do bus continua saudavel
// com o RabbitMQ parado (medido: 200 por 80 s).
//
// O broker fora responde Degraded, e nao Unhealthy: com o outbox transacional a API continua aceitando escrita (a
// mensagem espera no banco), entao tirar o pod de rotacao por causa do broker faria uma queda do RabbitMQ virar
// indisponibilidade total. Degraded mantem o /health/ready em 200 e aparece no corpo para quem monitora.
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private static readonly TimeSpan LimiteDaConexao = TimeSpan.FromSeconds(3);

    private readonly IOptions<OpcoesDoRabbitMq> _opcoes;

    public RabbitMqHealthCheck(IOptions<OpcoesDoRabbitMq> opcoes)
    {
        _opcoes = opcoes;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var rabbit = _opcoes.Value;
        var fabrica = new ConnectionFactory
        {
            HostName = rabbit.Host,
            Port = rabbit.Port,
            VirtualHost = rabbit.VirtualHost,
            ClientProvidedName = "OficinaMecanica.API.health",
            RequestedConnectionTimeout = LimiteDaConexao
        };

        // Sem usuario informado vale o padrao do ConnectionFactory (guest), o mesmo que o MassTransit usa.
        if (!string.IsNullOrEmpty(rabbit.Username)) fabrica.UserName = rabbit.Username;
        if (!string.IsNullOrEmpty(rabbit.Password)) fabrica.Password = rabbit.Password;

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(LimiteDaConexao);

        try
        {
            await using var conexao = await fabrica.CreateConnectionAsync(limite.Token);
            return HealthCheckResult.Healthy("Conexão AMQP com o RabbitMQ estabelecida.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("Não foi possível conectar ao RabbitMQ; as mensagens ficam no outbox até o broker voltar.", ex);
        }
    }
}
