using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Zoom;

/// <summary>Counter "zoomin.zoom.temporary_levels": owner and action per change (M17 step 0).</summary>
public sealed class TemporaryZoomMetricsTests
{
    private const string Left = @"\\.\DISPLAY1";
    private const string Right = @"\\.\DISPLAY2";

    [Fact]
    public void EveryChange_IsCounted_WithOwnerAndAction()
    {
        var recorded = new List<string>();

        // The meter is shared: other tests running in parallel record too. The callback runs on the
        // recording thread, so only this thread's calls are kept.
        var thread = Environment.CurrentManagedThreadId;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == Telemetry.Name && instrument.Name == "zoomin.zoom.temporary_levels")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            if (Environment.CurrentManagedThreadId == thread)
            {
                recorded.Add($"{tags[0].Value}:{tags[1].Value}:{value}");
            }
        });
        listener.Start();

        string[] monitors = [Left, Right];
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, perMonitor: true, 0.15);
        model.Toggle(Left, monitors);
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Left, 1);
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Left, 1.5); // replaced: not a new level
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Right, 1.2);
        model.Temporary.Move(TemporaryZoomOwner.WindowDrag, Left, Right); // merged: one level fewer
        model.Temporary.ReleaseAll(TemporaryZoomOwner.WindowDrag);
        model.Temporary.Set(TemporaryZoomOwner.Overview, Left, 1);
        model.Temporary.Release(TemporaryZoomOwner.Overview, Left);
        model.Temporary.Set(TemporaryZoomOwner.Overview, Left, 1);
        model.Toggle(Left, monitors);

        Assert.Equal(
            [
                "window_drag:set:1", "window_drag:set:1", "window_drag:move:1", "window_drag:release:1", "window_drag:release:1",
                "overview:set:1", "overview:release:1", "overview:set:1", "overview:cleared:1",
            ],
            recorded);
    }
}
