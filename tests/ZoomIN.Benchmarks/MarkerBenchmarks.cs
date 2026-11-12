using BenchmarkDotNet.Attributes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Benchmarks;

/// <summary>Highlight code the render loop calls every frame (M18 lane C). Budget: 0 B allocated.</summary>
public class MarkerBenchmarks
{
    private readonly PhysicalRect _monitor = new(0, 0, 3840, 2160);
    private readonly TrackingSnapshot _tracking = new(null, 0, new PhysicalRect(1500, 1200, 2, 30), 1);
    private readonly TrackingOptions _options = TrackingOptions.Default with
    {
        CaretStyle = CaretMarkerStyle.Default with { Shape = CaretMarkerShape.Wedges },
    };

    private readonly MarkerActivity _activity = new();
    private readonly MarkerAlphas _alphas = new();
    private readonly Core.Pointer.PointerStyle _cursor = Core.Pointer.PointerStyle.Default with
    {
        CrosshairEnabled = true,
        CrosshairShow = ShowCondition.Idle,
    };

    private readonly TrackingOptions _conditions = TrackingOptions.Default with
    {
        CaretShow = ShowCondition.Moving,
        FocusFrameShow = ShowCondition.HoldKey,
    };

    private int _frame;

    /// <summary>Snapshot read and geometry of every caret shape (HighlightLayer, E2).</summary>
    [Benchmark]
    public double CaretMarkerParts()
    {
        _frame++;
        if (!CaretMarkerGeometry.TryGetCaret(_tracking, _options, out var caret) || !CaretMarkerGeometry.InView(caret, _monitor))
        {
            return 0;
        }

        var shape = (CaretMarkerShape)(_frame % 4);
        var zoom = 1 + (_frame % 16);
        return CaretMarkerGeometry.Compute(caret, zoom, shape, _options.CaretStyle.Thickness, _monitor).First.X;
    }

    /// <summary>Activity read and the show-condition decision with fading for all three highlights (RenderSession, E1).</summary>
    [Benchmark]
    public double ShowConditions()
    {
        _frame++;
        if (_frame % 8 == 0)
        {
            _activity.Moved();
        }

        // The overload RenderSession calls, with every highlight on and a different condition each.
        _alphas.Update(_cursor, 4, _conditions, _activity.Sample,
            System.Diagnostics.Stopwatch.GetTimestamp(), System.Diagnostics.Stopwatch.Frequency);
        return _alphas.Crosshair + _alphas.Caret + _alphas.Focus;
    }
}
