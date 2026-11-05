using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.HotPath;

/// <summary>
/// The anchor switch counter is called from the render thread: with a listener attached it must
/// not allocate (AGENTS.md 2.3). The OpenTelemetry SDK path is checked in RenderMetricsTests.
/// </summary>
public sealed class AnchorMetricsAllocationTests
{
    [Fact]
    public void Switched_WithListener_DoesNotAllocate_AndIsCounted()
    {
        long counted = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == Telemetry.Name && instrument.Name == "zoomin.tracking.anchor_switches")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref counted, value));
        listener.Start();
        AnchorMetrics.Switched(AnchorSource.Focus); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            AnchorMetrics.Switched((AnchorSource)(i % 3));
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(10_001, Interlocked.Read(ref counted));
    }
}
