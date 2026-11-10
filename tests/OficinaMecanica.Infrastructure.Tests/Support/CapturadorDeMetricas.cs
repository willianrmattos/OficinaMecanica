using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// MeterListener de um Meter pelo nome: guarda cada medicao com as tags, para o teste afirmar o que foi emitido. O Meter da
// saga e estatico (como o MetricasNegocio), entao quem usa isto roda na colecao sem paralelismo (ColecaoDeMetricas): uma
// medicao de outro teste rodando ao mesmo tempo apareceria aqui.
public sealed class CapturadorDeMetricas : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<Medicao> _medicoes = new();

    public CapturadorDeMetricas(string nomeDoMeter)
    {
        _listener.InstrumentPublished = (instrumento, listener) =>
        {
            if (instrumento.Meter.Name == nomeDoMeter)
                listener.EnableMeasurementEvents(instrumento);
        };
        _listener.SetMeasurementEventCallback<long>((instrumento, valor, tags, _) => Guardar(instrumento, valor, tags));
        _listener.SetMeasurementEventCallback<double>((instrumento, valor, tags, _) => Guardar(instrumento, valor, tags));
        _listener.Start();
    }

    public IReadOnlyList<Medicao> Medicoes(string instrumento) =>
        _medicoes.Where(m => m.Instrumento == instrumento).ToList();

    public IReadOnlyList<Medicao> Todas => _medicoes.ToArray();

    // Gauges observaveis so emitem quando alguem pede a leitura (o exportador faz isso a cada ciclo).
    public void LerGauges() => _listener.RecordObservableInstruments();

    public void Dispose() => _listener.Dispose();

    private void Guardar(Instrument instrumento, double valor, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        _medicoes.Enqueue(new Medicao(instrumento.Name, valor, tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString())));
}

public sealed record Medicao(string Instrumento, double Valor, IReadOnlyDictionary<string, string?> Tags)
{
    public string? Tag(string chave) => Tags.TryGetValue(chave, out var valor) ? valor : null;
}

// As asserts sobre o Meter estatico da saga rodam em colecao que desliga o paralelismo: nenhum outro teste emite enquanto ela roda.
[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class ColecaoDeMetricas
{
    public const string Nome = "Metricas da saga (sem paralelismo)";
}
