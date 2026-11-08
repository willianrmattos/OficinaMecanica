using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Tests.Common.Fakes;

namespace OficinaMecanica.Integration.Tests.Support;

// A API de verdade (controller, MediatR, cliente HTTP tipado da Execucao com repasse do JWT e pipeline de resiliencia) com a rede
// da Execucao trocada por um manipulador falso: o teste diz o que a Execucao responde e le o que o OS Service enviou a ela.
public sealed class FabricaComExecucaoSimulada : CustomWebApplicationFactory
{
    private readonly int _minimoDeChamadasDoCircuito;

    // O circuito e do HttpClient nomeado, ou seja, da fabrica inteira: com varios testes na mesma fabrica, um 5xx abriria o circuito
    // dos testes seguintes. Por isso o padrao (a fabrica do IClassFixture) so abre depois de 1000 chamadas, e o teste do circuito
    // cria a fabrica dele com o minimo de 2.
    public FabricaComExecucaoSimulada() : this(1000)
    {
    }

    internal FabricaComExecucaoSimulada(int minimoDeChamadasDoCircuito)
    {
        _minimoDeChamadasDoCircuito = minimoDeChamadasDoCircuito;
    }

    public ManipuladorHttpFalso Execucao { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Limites curtos: o atraso do retry cai para 1 ms, entao os testes de retry nao esperam. O timeout continua em segundos
        // inteiros (o minimo da configuracao).
        builder.UseSetting("Execucao:BaseUrl", "http://execucao.teste");
        builder.UseSetting("Execucao:AtrasoDoRetryMs", "1");
        builder.UseSetting("Execucao:TimeoutSegundos", "2");
        builder.UseSetting("Execucao:TimeoutPorTentativaSegundos", "1");
        builder.UseSetting("Execucao:TentativasDeRetry", "1");
        builder.UseSetting("Execucao:CircuitoMinimoDeChamadas", _minimoDeChamadasDoCircuito.ToString());

        builder.ConfigureServices(servicos =>
            servicos.Configure<HttpClientFactoryOptions>(
                nameof(IExecucaoClient),
                opcoes => opcoes.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = Execucao)));
    }
}
