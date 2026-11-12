using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
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
        string[] monitors = [@"\\.\DISPLAY1", @"\\.\DISPLAY2"];
        model.Toggle(monitors[0], monitors);
        model.Temporary.Set(TemporaryZoomOwner.Overview, monitors[0], 1.5); // DISPLAY2 reads without one
        var sum = Read(model, monitors[0]) + Read(model, monitors[1]); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            sum += Read(model, monitors[i % 2]);
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
    public void AnchorController_FollowingFocusAndCaret_DoesNotAllocate()
    {
        var monitor = new PhysicalRect(0, 0, 1920, 1080);
        var tick = System.Diagnostics.Stopwatch.Frequency / 144;
        // Snapshots are published by the tracking thread, not created per frame: build them first.
        // Every few frames a new focus or caret position arrives, so all follow paths run.
        var snapshots = new TrackingSnapshot[Frames];
        for (var i = 0; i < Frames; i++)
        {
            snapshots[i] = (i / 4 % 3) switch
            {
                0 => new(new PhysicalRect(1500, 800, 100, 30), tick * (i / 4 * 4), null, 0),
                1 => new(null, 0, new PhysicalRect(200 + (i % 40), 900, 2, 20), tick * (i / 4 * 4)),
                _ => new(null, 0, new PhysicalRect(1700, 100, 2, 20), tick * (i / 4 * 4)),
            };
        }

        var controller = new AnchorController();
        var sum = Step(controller, monitor, 0, snapshots[0]); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 1; i < Frames; i++)
        {
            sum += Step(controller, monitor, tick * i, snapshots[i]);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum >= 0);
    }

    private static double Step(AnchorController controller, PhysicalRect monitor, long now, TrackingSnapshot snapshot) =>
        controller.Next(new AnchorInput(
            now, new PhysicalPoint(300, 300), true, monitor, (ZoomMode)(now % 3), 4, LensSize.Default, DockLayout.Default,
            snapshot, TrackingOptions.Default)).X;

    [Fact]
    public void PointerPlacement_DoesNotAllocate()
    {
        var monitor = new PhysicalRect(0, 0, 1920, 1080);
        var placement = PointerPlacement.Place(new PhysicalPoint(1, 1), 128, 128, 4, 4, monitor, default); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            placement = PointerPlacement.Place(new PhysicalPoint(i % 1920, i % 1080), 128, 128, 4, 4, monitor, placement);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(placement.X > -1000);
    }

    // Everything a render thread reads from the model per frame.
    private static double Read(ZoomModel model, string monitor) =>
        model.EffectiveTargetFor(monitor) + model.AnimationSeconds + (int)model.Mode + model.Lens.WidthFraction + model.Dock.Fraction;
}
