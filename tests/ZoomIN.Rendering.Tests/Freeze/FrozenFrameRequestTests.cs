using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.Freeze;

/// <summary>What a freeze means for the frame request and its change check (G2).</summary>
public sealed class FrozenFrameRequestTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 64, 64);

    [Fact]
    public void FrozenStill_IsDrawnAt1x_AndAFreezeIsAChangeOfTheFrame()
    {
        var request = new FrameRequest(ZoomMode.Fullscreen, ZoomMode.Fullscreen, 1, LensSize.Default, DockLayout.Default, DisplayEffects.Default,
            0, Core.Tracking.TrackingSnapshot.Empty, Core.Tracking.TrackingOptions.Default, false, WindowRegions.Empty, false);
        Assert.True(request.OverlayOnly);
        Assert.True((request with { Frozen = Frozen(still: false) }).OverlayOnly);
        Assert.False((request with { Frozen = Frozen(still: true), FrozenStill = true }).OverlayOnly);

        var tracker = new FrameChangeTracker();
        var frame = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, default, 1, LensSize.Default, DockLayout.Default);
        tracker.Presented(request, frame, false, default, 0);
        Assert.False(tracker.Compare(false, request, frame, false, default, 0, false, false, true).NeedsRedraw);
        var frozen = request with { Frozen = Frozen(still: false) };
        Assert.True(tracker.Compare(false, frozen, frame, false, default, 0, false, false, true).NeedsRedraw);

        // Frozen: a caret moving on another monitor is no reason to draw this one again.
        tracker.Presented(frozen, frame, false, default, 0);
        var typed = frozen with { Tracking = new Core.Tracking.TrackingSnapshot(null, 0, new PhysicalRect(1, 1, 1, 9), 5) };
        Assert.False(tracker.Compare(false, typed, frame, false, default, 0, false, false, true).NeedsRedraw);

        // A still image given up on during the same freeze shows live again: that is a change.
        var still = frozen with { FrozenStill = true };
        tracker.Presented(still, frame, false, default, 0);
        Assert.True(tracker.Compare(false, still with { FrozenStill = false }, frame, false, default, 0, false, false, true).NeedsRedraw);
    }

    private static FrozenMonitor Frozen(bool still) =>
        new(@"\\.\DISPLAY1", s_monitor, 2, ZoomMode.Fullscreen, LensSize.Default, DockLayout.Default, still, new RgbColor(0, 0, 255));
}
