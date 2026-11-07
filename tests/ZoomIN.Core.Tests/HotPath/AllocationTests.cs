using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.HotPath;

/// <summary>
/// Enforces "0 B per frame" (AGENTS.md 2.3) for the Core code the render loop calls.
/// Measured per thread, so parallel tests do not interfere.
/// </summary>
public sealed class AllocationTests
{
    private const int Frames = 10_000;

    [Fact]
    public void ViewportCalculator_DoesNotAllocate()
    {
        var monitor = new PhysicalRect(0, 0, 1920, 1080);
        var sum = 0.0;
        sum += ViewportCalculator.SourceRect(monitor, new PhysicalPoint(1, 1), 2).X; // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            sum += ViewportCalculator.SourceRect(monitor, new PhysicalPoint(i % 1920, i % 1080), 1 + (i % 16)).X;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum >= 0);
    }

    [Fact]
    public void ZoomAnimator_Advance_DoesNotAllocate()
    {
        var animator = new ZoomAnimator();
        animator.Advance(0.01, 0.15); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            animator.SetTarget(i % 2 == 0 ? 8 : 1);
            animator.Advance(1.0 / 144, 0.15);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ZoomModel_Reads_DoNotAllocate()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Lens, true, 0.15);
        string[] monitors = [@"\\.\DISPLAY1"];
        model.Toggle(monitors[0], monitors);
        var sum = Read(model, monitors[0]); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            sum += Read(model, monitors[0]);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    [Fact]
    public void ModeGeometry_DoesNotAllocate()
    {
        var monitor = new PhysicalRect(0, 0, 1920, 1080);
        var sum = ModeGeometry.Compute(ZoomMode.Lens, monitor, new PhysicalPoint(5, 5), 2, LensSize.Default, DockLayout.Default).Target.X;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            var mode = (ZoomMode)(i % 3);
            sum += ModeGeometry.Compute(mode, monitor, new PhysicalPoint(i % 1920, i % 1080), 1 + (i % 8), LensSize.Default, DockLayout.Default).Source.X;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum >= 0);
    }

    [Fact]
    public void AnchorAndColorRegion_WithNotification_DoNotAllocate()
    {
        var monitor = new PhysicalRect(0, 0, 1920, 1080);
        var options = TrackingOptions.Default with { PanSeconds = 0.2 };
        // Snapshots are built by the tracking thread, not the render thread: created up front.
        var toast = new TrackingSnapshot(null, 0, null, 0, new PhysicalRect(1540, 820, 360, 200), 2);
        var later = toast with { NotificationChangedAt = 3 };
        var controller = new AnchorController();
        var sum = controller.Next(Anchor(1, monitor, TrackingSnapshot.Empty, options)).X; // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            // Toast appears and closes again every 100 frames; the hotkey fires in between.
            var tracking = i % 100 >= 50 ? TrackingSnapshot.Empty : i % 200 < 100 ? toast : later;
            sum += controller.Next(Anchor(2 + (i * 1000L), monitor, tracking, options, i % 100 == 25 ? i : 0)).X;
            sum += ColorRegion.For(tracking, options)?.Area.Width ?? 0;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum >= 0);
    }

    private static AnchorInput Anchor(long now, PhysicalRect monitor, TrackingSnapshot tracking, TrackingOptions options, long requestedAt = 0) =>
        new(now, new PhysicalPoint(300, 300), true, monitor, ZoomMode.Fullscreen, 4, LensSize.Default, DockLayout.Default,
            tracking, options, requestedAt);

    // Everything a render thread reads from the model per frame.
    private static double Read(ZoomModel model, string monitor) =>
        model.TargetFor(monitor) + model.AnimationSeconds + (int)model.Mode + model.Lens.WidthFraction + model.Dock.Fraction;
}
