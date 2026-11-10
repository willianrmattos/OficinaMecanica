using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// O laco do BackgroundService com o relogio controlado: o ciclo (PeriodicTimer) avanca com FakeTimeProvider.Advance, sem esperar
// segundos de verdade.
public class SweeperDePrazosBackgroundServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly PrazoRepositoryEmMemoria _prazos = new();
    private readonly PublicadorDeTeste _publicador = new();
    private readonly FakeTimeProvider _relogio = new(Agora);
    private readonly LoggerCapturador _logs = new();
    private ServiceProvider? _provedor;
    private SweeperDePrazosBackgroundService? _servico;

    private SweeperDePrazosBackgroundService CriarServico(SagaOptions? opcoes = null)
    {
        var optionsDoServico = Options.Create(opcoes ?? new SagaOptions { SweeperIntervaloSegundos = 30, SweeperLote = 2 });

        var services = new ServiceCollection();
        services.AddLogging(l => { l.ClearProviders(); l.AddProvider(_logs); });
        services.AddSingleton<IPrazoRepository>(_prazos);
        services.AddSingleton<IIntegrationEventPublisher>(_publicador);
        services.AddSingleton<TimeProvider>(_relogio);
        services.AddSingleton(optionsDoServico);
        services.AddScoped<SweeperDePrazos>();
        _provedor = services.BuildServiceProvider(validateScopes: true);

        _servico = new SweeperDePrazosBackgroundService(
            _provedor.GetRequiredService<IServiceScopeFactory>(), _relogio, optionsDoServico,
            _provedor.GetRequiredService<ILogger<SweeperDePrazosBackgroundService>>());
        return _servico;
    }

    private async Task AgendarVencidosAsync(int quantidade)
    {
        for (var i = 0; i < quantidade; i++)
            await _prazos.AdicionarAsync(new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Aprovacao, Agora.AddMinutes(-i - 1)));
    }

    [Fact]
    public async Task Servico_AoIniciar_DeveVarrerNaHoraSemEsperarOPrimeiroIntervalo()
    {
        await AgendarVencidosAsync(1);
        var servico = CriarServico();

        await servico.StartAsync(CancellationToken.None);

        (await Esperar.AteAsync(() => _publicador.Publicadas.Count == 1)).Should().BeTrue();
        await servico.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Servico_ACadaIntervalo_DeveVarrerDeNovo()
    {
        var servico = CriarServico();
        await servico.StartAsync(CancellationToken.None);
        (await Esperar.AteAsync(() => _prazos.Chamadas.Count == 1)).Should().BeTrue();
        await _prazos.AdicionarAsync(new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), TipoPrazo.Pagamento, Agora.AddSeconds(10)));

        _relogio.Advance(TimeSpan.FromSeconds(29));
        await Task.Delay(100);
        _prazos.Chamadas.Should().HaveCount(1, "o intervalo e de 30 segundos");

        _relogio.Advance(TimeSpan.FromSeconds(1));

        (await Esperar.AteAsync(() => _publicador.Publicadas.Count == 1)).Should().BeTrue();
        await servico.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Servico_ComMaisVencidosQueOLote_DeveContinuarAteEsvaziarSemEsperarOProximoCiclo()
    {
        await AgendarVencidosAsync(5);
        var servico = CriarServico();

        await servico.StartAsync(CancellationToken.None);

        (await Esperar.AteAsync(() => _publicador.Publicadas.Count == 5)).Should().BeTrue("lote cheio indica mais vencidos: segue na hora");
        _prazos.Chamadas.Select(c => c.Lote).Should().OnlyContain(l => l == 2);
        await servico.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Servico_ComFalhaNaVarredura_DeveLogarEContinuarNoProximoCiclo()
    {
        _prazos.FalharAoProcessar = new InvalidOperationException("banco fora do ar");
        var servico = CriarServico();
        await servico.StartAsync(CancellationToken.None);
        (await Esperar.AteAsync(() => _logs.Registros.Any(r => r.Nivel == LogLevel.Error))).Should().BeTrue();

        _prazos.FalharAoProcessar = null;
        await AgendarVencidosAsync(1);
        _relogio.Advance(TimeSpan.FromSeconds(30));

        (await Esperar.AteAsync(() => _publicador.Publicadas.Count == 1)).Should().BeTrue("uma falha nao derruba o laco");
        _logs.Registros.Should().Contain(r => r.Nivel == LogLevel.Error && r.Excecao is InvalidOperationException && r.Mensagem.Contains("varredura de prazos"));
        await servico.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Servico_Desabilitado_NaoDeveVarrerNada()
    {
        await AgendarVencidosAsync(1);
        var servico = CriarServico(new SagaOptions { SweeperHabilitado = false });

        await servico.StartAsync(CancellationToken.None);
        _relogio.Advance(TimeSpan.FromHours(1));
        await Task.Delay(100);
        await servico.StopAsync(CancellationToken.None);

        _prazos.Chamadas.Should().BeEmpty();
        _publicador.Publicadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Servico_AoParar_DeveTerminarSemErro()
    {
        var servico = CriarServico();
        await servico.StartAsync(CancellationToken.None);
        (await Esperar.AteAsync(() => _prazos.Chamadas.Count >= 1)).Should().BeTrue();

        var parar = async () => await servico.StopAsync(CancellationToken.None);

        await parar.Should().NotThrowAsync();
        servico.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    public async ValueTask DisposeAsync()
    {
        _servico?.Dispose();
        if (_provedor is not null)
            await _provedor.DisposeAsync();
    }
}
