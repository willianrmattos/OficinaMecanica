using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace OficinaMecanica.Bdd.Tests.Support;

// MeterListener de um Meter pelo nome, um por cenario. O Meter da saga e estatico, mas este projeto roda sem paralelismo
// (ver AssemblyInfo), entao so o cenario corrente emite enquanto o listener existe.
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

    public double Somar(string instrumento, string? tag = null, string? valorDaTag = null) =>
        _medicoes
            .Where(m => m.Instrumento == instrumento && (tag is null || m.Tags.GetValueOrDefault(tag) == valorDaTag))
            .Sum(m => m.Valor);

    public void Dispose() => _listener.Dispose();

    private void Guardar(Instrument instrumento, double valor, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        _medicoes.Enqueue(new Medicao(instrumento.Name, valor, tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString())));

    private sealed record Medicao(string Instrumento, double Valor, IReadOnlyDictionary<string, string?> Tags);
}
