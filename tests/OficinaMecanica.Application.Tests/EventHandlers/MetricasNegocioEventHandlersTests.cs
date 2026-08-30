using System.Diagnostics.Metrics;
using FluentAssertions;
using OficinaMecanica.Application.EventHandlers;
using OficinaMecanica.Application.Observabilidade;
using OficinaMecanica.Domain.Events;

namespace OficinaMecanica.Application.Tests.EventHandlers;

// Counter<T>/Histogram<T> nao tem getter pra "valor atual" - a API de Metrics
// foi desenhada pra ser observada externamente (OTLP, etc.), nao consultada.
// Por isso preciso do MeterListener pra capturar as medicoes emitidas durante
// o teste. Os instrumentos de MetricasNegocio sao estaticos e compartilhados
// por todo o assembly de teste, entao escopo cada listener com "using" (dura
// so o teste) e filtro estritamente por instrument.Name - nao so por
// instrument.Meter.Name - pra nao pegar medicao de instrumento nenhum outro
// que porventura exista no mesmo Meter.
public class OrdemDeServicoCriadaMetricaEventHandlerTests
{
    [Fact]
    public async Task Handle_OrdemDeServicoCriada_DeveIncrementarContadorOrdensCriadas()
    {
        var medicoes = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == MetricasNegocio.MeterName && instrument.Name == "ordens_criadas_total")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            medicoes.Add((measurement, tags.ToArray()));
        });
        listener.Start();

        var handler = new OrdemDeServicoCriadaMetricaEventHandler();
        var notification = new OrdemDeServicoCriadaEvent(Guid.NewGuid(), "OS-20260101-ABCDEF", Guid.NewGuid());

        await handler.Handle(notification, CancellationToken.None);

        medicoes.Should().ContainSingle(m => m.Value == 1);
    }
}

public class OrcamentoRecusadoMetricaEventHandlerTests
{
    [Fact]
    public async Task Handle_OrcamentoRecusadoComMotivo_DeveIncrementarContadorComTagDoMotivoInformado()
    {
        var medicoes = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == MetricasNegocio.MeterName && instrument.Name == "orcamentos_recusados_total")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            medicoes.Add((measurement, tags.ToArray()));
        });
        listener.Start();

        var handler = new OrcamentoRecusadoMetricaEventHandler();
        var notification = new OrcamentoRecusadoEvent(Guid.NewGuid(), "OS-20260101-ABCDEF", "Cliente desistiu do reparo");

        await handler.Handle(notification, CancellationToken.None);

        medicoes.Should().ContainSingle(m =>
            m.Value == 1 && m.Tags.Any(t => t.Key == "motivo" && (string?)t.Value == "Cliente desistiu do reparo"));
    }

    [Fact]
    public async Task Handle_OrcamentoRecusadoSemMotivo_DeveIncrementarContadorComTagNaoInformado()
    {
        var medicoes = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == MetricasNegocio.MeterName && instrument.Name == "orcamentos_recusados_total")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            medicoes.Add((measurement, tags.ToArray()));
        });
        listener.Start();

        var handler = new OrcamentoRecusadoMetricaEventHandler();
        var notification = new OrcamentoRecusadoEvent(Guid.NewGuid(), "OS-20260101-ABCDEF", motivo: null);

        await handler.Handle(notification, CancellationToken.None);

        medicoes.Should().ContainSingle(m =>
            m.Value == 1 && m.Tags.Any(t => t.Key == "motivo" && (string?)t.Value == "nao_informado"));
    }
}
