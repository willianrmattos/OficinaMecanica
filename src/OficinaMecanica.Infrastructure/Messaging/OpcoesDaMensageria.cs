namespace OficinaMecanica.Infrastructure.Messaging;

public enum TransporteDaMensageria
{
    RabbitMq = 1,
    EmMemoria = 2
}

public enum PersistenciaDaMensageria
{
    EntityFramework = 1,
    Nenhuma = 2
}

// Secao "Mensageria". Transporte e Persistencia sao o ponto de troca dos testes: o EF InMemory nao suporta o
// outbox transacional e os testes que nao sobem Docker nao tem RabbitMQ, entao eles trocam o RabbitMQ pelo
// transporte em memoria e o outbox do EF por nada. Em producao os padroes ja sao os corretos.
public sealed class OpcoesDaMensageria
{
    public const string Secao = "Mensageria";

    private static readonly int[] IntervalosDeRetryPadraoEmMs = { 500, 2_000, 10_000, 30_000, 60_000 };

    public TransporteDaMensageria Transporte { get; set; } = TransporteDaMensageria.RabbitMq;
    public PersistenciaDaMensageria Persistencia { get; set; } = PersistenciaDaMensageria.EntityFramework;

    // Cada intervalo e uma nova tentativa da mensagem inteira, em transacao nova; depois do ultimo ela vai para a
    // fila _error. Optei por deixar nulo em vez de ja trazer o padrao: o binder da configuracao ACRESCENTA os itens
    // lidos a um array que ja tenha valor, e o padrao se misturaria com o que vier do appsettings ou do ambiente.
    public int[]? RetryIntervalosMs { get; set; }

    // Tempo entre as varreduras do outbox pelo servico de entrega; o padrao do MassTransit e 1 segundo.
    public int OutboxQueryDelayMs { get; set; } = 1_000;

    public int TimeoutDeInicioSegundos { get; set; } = 60;

    // Valores aceitos de Transporte e Persistencia, e a mensagem de quem recebe um numero fora do enum. Optei por
    // lancar em vez de cair num ramo padrao: o binder aceita "0" ou "9" para um enum, e tratar "o que nao e RabbitMq"
    // como memoria sobe a API sem broker (ou sem outbox) em silencio.
    public const string TransportesAceitos = "RabbitMq ou EmMemoria";
    public const string PersistenciasAceitas = "EntityFramework ou Nenhuma";

    public static string MensagemDeValorInvalido(string chave, int valor, string aceitos) =>
        $"{Secao}:{chave} tem o valor inválido '{valor}'. Valores aceitos: {aceitos}.";

    public bool UsaRabbitMq => Transporte switch
    {
        TransporteDaMensageria.RabbitMq => true,
        TransporteDaMensageria.EmMemoria => false,
        _ => throw new InvalidOperationException(MensagemDeValorInvalido(nameof(Transporte), (int)Transporte, TransportesAceitos))
    };

    public bool UsaOutboxDoEntityFramework => Persistencia switch
    {
        PersistenciaDaMensageria.EntityFramework => true,
        PersistenciaDaMensageria.Nenhuma => false,
        _ => throw new InvalidOperationException(MensagemDeValorInvalido(nameof(Persistencia), (int)Persistencia, PersistenciasAceitas))
    };

    public TimeSpan[] IntervalosDeRetry() =>
        (RetryIntervalosMs is { Length: > 0 } ? RetryIntervalosMs : IntervalosDeRetryPadraoEmMs)
            .Select(ms => TimeSpan.FromMilliseconds(ms))
            .ToArray();
}

public sealed class OpcoesDoRabbitMq
{
    public const string Secao = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string? Username { get; set; }
    public string? Password { get; set; }
}
