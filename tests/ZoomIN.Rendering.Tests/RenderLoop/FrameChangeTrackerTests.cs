using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>Idle frames (M14 step 3): which differences to the last presented frame force a new one.</summary>
public sealed class FrameChangeTrackerTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly PhysicalPoint s_cursor = new(500, 400);
    private static readonly ModeFrame s_frame = new(s_monitor, new PhysicalRect(250, 200, 960, 540), false);
    private static readonly FrameRequest s_request = new(
        ZoomMode.Fullscreen, ZoomMode.Fullscreen, 2, LensSize.Default, DockLayout.Default, DisplayEffects.Default, 0, TrackingSnapshot.Empty,
        TrackingOptions.Default, false, WindowRegions.Empty, false);

    [Fact]
    public void BeforeTheFirstFrame_Redraws()
    {
        var changes = new FrameChangeTracker().Compare(false, s_request, s_frame, true, s_cursor, 1, false, false, false);

        Assert.True(changes.FirstFrame);
    }

    [Fact]
    public void SameEverything_IsIdle()
    {
        Assert.False(Presented().Compare(false, s_request, s_frame, true, s_cursor, 1, false, false, true).NeedsRedraw);
    }

    [Fact]
    public void HiddenOverlay_Redraws()
    {
        Assert.True(Presented().Compare(false, s_request, s_frame, true, s_cursor, 1, false, false, shown: false).FirstFrame);
    }

    [Fact]
    public void PointerMoveOrShape_OnThisMonitor_Redraws()
    {
        var tracker = Presented();

        Assert.True(tracker.Compare(false, s_request, s_frame, true, s_cursor with { X = 501 }, 1, false, false, true).PointerChanged);
        Assert.True(tracker.Compare(false, s_request, s_frame, true, s_cursor, 2, false, false, true).PointerChanged);
        Assert.True(tracker.Compare(false, s_request, s_frame, false, s_cursor, 1, false, false, true).PointerChanged);
    }

    [Fact]
    public void PointerMove_OnAnotherMonitor_ChangesNothing()
    {
        var tracker = new FrameChangeTracker();
        tracker.Presented(s_request, s_frame, false, s_cursor, 1);

        Assert.False(tracker.Compare(false, s_request, s_frame, false, s_cursor with { X = 3000 }, 2, false, false, true).NeedsRedraw);
    }

    [Fact]
    public void NewSnapshots_Redraw()
    {
        var tracker = Presented();

        Assert.True(Changed(tracker, s_request with { Effects = DisplayEffects.Default with { Smoothing = Smoothing.Pixel } }));
        Assert.True(Changed(tracker, s_request with { Tracking = new TrackingSnapshot(null, 1, null, 0) }));
        Assert.True(Changed(tracker, s_request with { Windows = new WindowRegions([s_monitor], [WindowPalette.Smart]) }));
        Assert.True(Changed(tracker, s_request with { Zoom = 2.01 }));
        Assert.True(Changed(tracker, s_request with { LocateRequestedAt = 5 }));
        Assert.True(Changed(tracker, s_request with { PointerHidden = true }));
    }

    [Fact]
    public void ViewMove_NewImage_AnimationOrSnapshot_Redraw()
    {
        var tracker = Presented();
        var moved = s_frame with { Source = new PhysicalRect(251, 200, 960, 540) };

        Assert.True(tracker.Compare(false, s_request, moved, true, s_cursor, 1, false, false, true).ViewChanged);
        Assert.True(tracker.Compare(true, s_request, s_frame, true, s_cursor, 1, false, false, true).NewDesktopImage);
        Assert.True(tracker.Compare(false, s_request, s_frame, true, s_cursor, 1, true, false, true).Animating);
        Assert.True(tracker.Compare(false, s_request, s_frame, true, s_cursor, 1, false, true, true).SnapshotRequested);
    }

    [Fact]
    public void Compare_DoesNotAllocate()
    {
        var tracker = Presented();
        tracker.Compare(false, s_request, s_frame, true, s_cursor, 1, false, false, true);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            tracker.Compare(false, s_request, s_frame, true, s_cursor with { X = i }, 1, false, false, true);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static FrameChangeTracker Presented()
    {
        var tracker = new FrameChangeTracker();
        tracker.Presented(s_request, s_frame, true, s_cursor, 1);
        return tracker;
    }

    private static bool Changed(FrameChangeTracker tracker, in FrameRequest request) =>
        tracker.Compare(false, request, s_frame, true, s_cursor, 1, false, false, true).RequestChanged;
}
