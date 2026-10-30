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
        var model = new ZoomModel(2, new Dictionary<string, double>(), true, 0.15);
        string[] monitors = [@"\\.\DISPLAY1"];
        model.Toggle(monitors);
        var sum = model.TargetFor(monitors[0]) + model.AnimationSeconds; // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            sum += model.TargetFor(monitors[0]) + model.AnimationSeconds;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }
}
