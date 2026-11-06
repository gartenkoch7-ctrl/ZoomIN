using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Smart inversion runs every frame: weights, check and draw must not allocate (AGENTS.md 2.3).</summary>
public sealed class SmartInvertAllocationTests
{
    private const int Size = 64;

    [Fact]
    public void PrepareCheckAndDraw_DoNotAllocate()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        using var ambient = new AmbientPass(gpu.Device, Size, Size);
        var layer = new SmartInvertLayer(ambient, @"\\.\DISPLAY1");
        var monitor = new PhysicalRect(0, 0, Size, Size);
        var effects = new DisplayEffects(new FilterSettings(true, ColorFilter.InvertedSmart, 1, 0), Smoothing.Text, PointerStyle.Default);
        var regions = new WindowRegions([new PhysicalRect(0, 0, 32, 32)], [WindowPalette.Smart]);
        var constants = MagnifyConstants.Fullscreen(monitor, monitor, effects.Matrix, Smoothing.Text, smart: true);

        // Warm-up / JIT.
        var weights = layer.Prepare(gpu.Context, image.View, regions.UsesSmart(monitor), effects.ProtectPhotos);
        pass.Draw(gpu.Context, image.View, target.View, Size, Size, constants, weights);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            var needed = effects.IsSmart || regions.UsesSmart(monitor) || regions.ChangesMonitor(monitor, effects);
            weights = layer.Prepare(gpu.Context, image.View, needed, effects.ProtectPhotos);
            pass.Draw(gpu.Context, image.View, target.View, Size, Size, constants, weights);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(0.016, true, 0.27, 0.28)]   // one 60 Hz frame
    [InlineData(0.150, true, 0.95, 0.96)]   // ~95 % after 150 ms, whatever the frame rate
    [InlineData(10.0, true, 0.999, 1.0)]    // long gap: the old weights are gone
    [InlineData(0.016, false, 1.0, 1.0)]    // first frame after a pause: no memory at all
    [InlineData(-1.0, true, 0.0, 0.0)]      // clock oddity never mixes in more than the new weights
    public void Alpha_FollowsTheTimeConstant(double seconds, bool running, double low, double high)
    {
        Assert.InRange(SmartInvertLayer.AlphaAfter(seconds, running), low, high);
    }
}
