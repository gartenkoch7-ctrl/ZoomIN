using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Tests.Pointer;

/// <summary>The hook only counts; an SDK listener reads both counters (A6) through their callbacks.</summary>
public sealed class PointerConstraintMetricsTests
{
    [Fact]
    public void ObservableCounters_ReportTheCounts()
    {
        PointerConstraintMetrics.Blocked();
        PointerConstraintMetrics.Locked(LockAxis.Horizontal);
        PointerConstraintMetrics.Locked(LockAxis.Vertical);
        var seen = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == Telemetry.Meter.Name && instrument.Name.StartsWith("zoomin.pointer.", StringComparison.Ordinal))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var key = tags.Length > 0 ? $"{instrument.Name}/{tags[0].Value}" : instrument.Name;
            seen[key] = value;
        });
        listener.Start();

        listener.RecordObservableInstruments();

        Assert.True(seen["zoomin.pointer.buffer_blocked"] >= 1);
        Assert.True(seen["zoomin.pointer.axis_lock/horizontal"] >= 1);
        Assert.True(seen["zoomin.pointer.axis_lock/vertical"] >= 1);
    }
}
