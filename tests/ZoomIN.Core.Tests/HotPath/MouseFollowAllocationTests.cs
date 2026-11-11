using System.Diagnostics;
using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.HotPath;

/// <summary>
/// Mouse follow (M17 A1) runs on the render thread every frame: every mode, the margin push, zoom
/// changes, placements and the push counter with a listener attached must allocate nothing.
/// </summary>
public sealed class MouseFollowAllocationTests
{
    private const int Frames = 10_000;

    // Options come from the UI thread as a snapshot, not per frame: build them first.
    private static readonly TrackingOptions[] s_options = Enumerable.Range(0, 15)
        .Select(i => TrackingOptions.Default with { MouseFollow = (MouseFollowMode)(i % 3), MouseMargin = (i % 5) / 10.0 })
        .ToArray();

    [Fact]
    public void FollowingTheMouse_InEveryMode_DoesNotAllocate()
    {
        long pushes = 0;
        var thread = Environment.CurrentManagedThreadId;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == Telemetry.Name && instrument.Name == "zoomin.tracking.mouse_push")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
        {
            if (Environment.CurrentManagedThreadId == thread)
            {
                Interlocked.Add(ref pushes, value);
            }
        });
        listener.Start();

        var monitor = new PhysicalRect(0, 0, 1920, 1080);
        // Warm-up: JIT, static instruments and the listener's first measurement of each tag.
        var warmUp = new AnchorController();
        var sum = 0.0;
        for (var i = 0; i < Frames; i++)
        {
            sum += Step(warmUp, monitor, i);
        }

        var controller = new AnchorController();
        sum += Step(controller, monitor, 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 1; i < Frames; i++)
        {
            sum += Step(controller, monitor, i);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum >= 0);
        Assert.True(Interlocked.Read(ref pushes) > 0, "the mouse pushed the view");
    }

    private static double Step(AnchorController controller, PhysicalRect monitor, int i)
    {
        var tick = Stopwatch.Frequency / 144;

        // A serpentine mouse path, the zoom gliding up and down, every mode and follow mode, and
        // now and then a placement.
        var x = (i * 7) % 3800;
        var mouse = new PhysicalPoint(x < 1900 ? x : 3800 - x, (i * 3) % 1080);
        var zoom = 1 + ((i / 50) % 8);
        var options = s_options[i / 500 % s_options.Length];
        var placement = new ViewPlacement(new PhysicalPoint(800, 400), 4, tick * (i / 700 * 700));
        return controller.Next(new AnchorInput(
            tick * i, mouse, true, monitor, (ZoomMode)(i / 1000 % 3), zoom, LensSize.Default, DockLayout.Default,
            TrackingSnapshot.Empty, options, placement)).X;
    }
}
