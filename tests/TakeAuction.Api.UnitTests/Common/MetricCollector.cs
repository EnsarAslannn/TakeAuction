using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace TakeAuction.Api.UnitTests.Common;

public sealed record Measurement(string Instrument, double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)
{
    public bool Tagged(string key, object value) =>
        Tags.Any(tag => tag.Key == key && Equals(tag.Value, value));
}

/// <summary>
/// Listens to a meter the way a collector would, so a test can assert on what the process
/// actually published rather than on the call that was supposed to publish it.
/// </summary>
public sealed class MetricCollector : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<Measurement> _measurements = new();

    /// <summary>
    /// Bound to the meter instance, not to its name: every test builds its own, and matching
    /// on the name would let a test running alongside pour its measurements into this one.
    /// </summary>
    public MetricCollector(Meter meter)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, meter))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));

        _listener.Start();
    }

    public IReadOnlyList<Measurement> Measurements => [.. _measurements];

    public IReadOnlyList<Measurement> For(string instrument) =>
        [.. _measurements.Where(measurement => measurement.Instrument == instrument)];

    public double Total(string instrument) => For(instrument).Sum(measurement => measurement.Value);

    public void Dispose() => _listener.Dispose();

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        _measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray()));
}
