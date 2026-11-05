using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>
/// The render loop records every frame: with the OpenTelemetry SDK listening (level "Verbose" with
/// an export address) that must still allocate nothing (AGENTS.md 2.3, ADR-0019).
/// </summary>
public sealed class RenderMetricsTests
{
    private const string Monitor = @"\\.\DISPLAY1";

    [Fact]
    public void Recording_WithTheSdkListening_AllocatesNothing()
    {
        using var meters = Sdk.CreateMeterProviderBuilder()
            .AddMeter(Telemetry.Name)
            .AddReader(new BaseExportingMetricReader(new DiscardingExporter()))
            .Build();
        var metrics = new RenderMetrics(Monitor);
        Record(metrics, 1_000); // warm-up: JIT and the SDK's first lookup of the tag set

        var before = GC.GetAllocatedBytesForCurrentThread();
        Record(metrics, 10_000);

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Recording_ReachesListeners_WithTheMonitorTag()
    {
        var seen = new List<(string Instrument, string? Monitor)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == Telemetry.Name && instrument.Name.StartsWith("zoomin.render.", StringComparison.Ordinal))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => seen.Add((instrument.Name, MonitorOf(tags))));
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => seen.Add((instrument.Name, MonitorOf(tags))));
        listener.Start();

        var metrics = new RenderMetrics(Monitor);
        metrics.FramePresented(16.7);
        metrics.FrameMissed();
        metrics.Shown(12);
        metrics.Restarted("failure");

        Assert.Equal(
            ["zoomin.render.frame_interval", "zoomin.render.frames_missed", "zoomin.render.show_latency", "zoomin.render.pipeline_restarts"],
            seen.Select(s => s.Instrument));
        Assert.All(seen, s => Assert.Equal(Monitor, s.Monitor));
    }

    private static void Record(RenderMetrics metrics, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            metrics.FramePresented(16.6 + (i % 3));
            metrics.FrameMissed();
        }
    }

    private static string? MonitorOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == RenderMetrics.MonitorTag)
            {
                return tag.Value as string;
            }
        }

        return null;
    }

    private sealed class DiscardingExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch) => ExportResult.Success;
    }
}
