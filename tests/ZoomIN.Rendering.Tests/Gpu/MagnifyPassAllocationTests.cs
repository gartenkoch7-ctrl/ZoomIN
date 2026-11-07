using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>The draw call runs every frame: it must not allocate (AGENTS.md 2.3).</summary>
public sealed class MagnifyPassAllocationTests
{
    private const int Size = 64;

    [Fact]
    public void Draw_DoesNotAllocate()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        var monitor = new PhysicalRect(0, 0, Size, Size);
        var constants = MagnifyConstants.FromSourceRect(new PhysicalRect(8, 8, 32, 32), monitor);
        pass.Draw(gpu.Context, image.View, target.View, Size, Size, constants); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            constants = MagnifyConstants.FromSourceRect(new PhysicalRect(i % 32, 0, 32, 32), monitor);
            pass.Draw(gpu.Context, image.View, target.View, Size, Size, constants);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Draw_WithColorRegion_DoesNotAllocate()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        var monitor = new PhysicalRect(0, 0, Size, Size);
        var color = ColorFilters.For(ColorFilter.Inverted);
        ColorRegion? toast = new ColorRegion(new PhysicalRect(40, 40, 20, 16), ColorMatrix.Identity);
        var constants = MagnifyConstants.Fullscreen(monitor, monitor, color, Smoothing.Text, toast);
        pass.Draw(gpu.Context, image.View, target.View, Size, Size, constants); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            constants = MagnifyConstants.Fullscreen(
                new PhysicalRect(i % 32, 0, 32, 32), monitor, color, Smoothing.Text, i % 2 == 0 ? toast : null);
            pass.Draw(gpu.Context, image.View, target.View, Size, Size, constants);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
