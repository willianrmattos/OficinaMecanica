namespace OficinaMecanica.Infrastructure.Execucao;

// Secao "Execucao" (variaveis Execucao__*): como o OS Service fala com a Execucao na consulta sincrona. Em producao so
// BaseUrl muda (Execucao__BaseUrl, o Service do cluster); os limites tem padrao pensado para uma consulta de leitura
// que nunca pode segurar a requisicao do usuario por mais que alguns segundos.
public sealed class OpcoesDaExecucao
{
    public const string Secao = "Execucao";

    public string BaseUrl { get; set; } = "http://localhost:5002";

    // Teto de tudo (todas as tentativas e as esperas entre elas): a resposta ao usuario nao passa disto, vira fallback.
    public int TimeoutSegundos { get; set; } = 6;

    // Teto de UMA tentativa: uma Execucao travada gasta isto e nao o teto total inteiro.
    public int TimeoutPorTentativaSegundos { get; set; } = 2;

    // Repeticoes depois da primeira tentativa (so GET, so 5xx, timeout ou falha de rede). O Polly exige pelo menos 1.
    public int TentativasDeRetry { get; set; } = 2;

    // Atraso base do retry; cresce de forma exponencial e com jitter para varias replicas nao repetirem juntas.
    public int AtrasoDoRetryMs { get; set; } = 200;

    // Circuit breaker: abre quando, na janela, a proporcao de falhas passa da taxa e houve ao menos o minimo de chamadas.
    public double CircuitoTaxaDeFalha { get; set; } = 0.5;
    public int CircuitoMinimoDeChamadas { get; set; } = 5;
    public int CircuitoJanelaSegundos { get; set; } = 30;

    // Quanto o circuito fica aberto antes de deixar uma chamada de teste passar.
    public int CircuitoPausaSegundos { get; set; } = 30;
}
