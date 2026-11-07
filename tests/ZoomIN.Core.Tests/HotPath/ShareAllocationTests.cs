using ZoomIN.Core.Sharing;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.HotPath;

/// <summary>"0 B per frame" (AGENTS.md 2.3) for what the render loop reads of the share filter.</summary>
public sealed class ShareAllocationTests
{
    private const int Frames = 10_000;

    [Fact]
    public void ShareState_ReadAndClip_DoesNotAllocate()
    {
        var model = new ShareModel(ShareOptions.Default);
        model.ToggleWindow(42, new PhysicalRect(1520, 100, 800, 600));
        var monitor = new PhysicalRect(0, 0, 1920, 1080);
        var sum = Read(model, monitor); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            sum += Read(model, monitor);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    private static double Read(ShareModel model, PhysicalRect monitor)
    {
        var state = model.Current;
        return ShareGeometry.TryClip(state.Area, monitor, out var clipped)
            ? clipped.Width + state.Matrix.M00
            : 0;
    }
}
