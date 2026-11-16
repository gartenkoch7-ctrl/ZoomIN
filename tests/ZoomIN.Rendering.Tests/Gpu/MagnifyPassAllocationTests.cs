using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
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
    public void WindowRegions_FillUploadAndDraw_DoNotAllocate() => AssertFillUploadDrawFree(DisplayEffects.Default, WindowPalette.For(FilterId.Inverted));

    [Fact]
    public void OwnSchemesWithReplacements_FillUploadAndDraw_DoNotAllocate()
    {
        var replacements = new[] { new ColorReplacement(), new ColorReplacement { From = "#00FF00" }, new ColorReplacement { From = "#0000FF" } };
        var schemes = Enumerable.Range(0, CustomSchemes.Max)
            .Select(i => new CustomScheme { Id = CustomSchemes.IdOf(i), Kind = (SchemeKind)i, Replacements = replacements })
            .ToList();
        var effects = new DisplayEffects(new FilterSettings(true, FilterId.Custom2, 1, 0), Smoothing.Text, PointerStyle.Default)
        {
            Schemes = schemes,
        };

        AssertFillUploadDrawFree(effects, WindowPalette.For(FilterId.Custom4));
    }

    private static void AssertFillUploadDrawFree(DisplayEffects effects, byte entry)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        var monitor = new PhysicalRect(0, 0, Size, Size);
        var rects = new PhysicalRect[WindowRegions.Max];
        var palette = new byte[WindowRegions.Max];
        for (var i = 0; i < rects.Length; i++)
        {
            rects[i] = new PhysicalRect(i, i, 20, 20);
            palette[i] = entry;
        }

        var regions = new WindowRegions(rects, palette);
        var windows = default(WindowConstants);
        var constants = MagnifyConstants.Fullscreen(monitor, monitor, effects.Matrix, Smoothing.Text);
        WindowConstants.Fill(ref windows, regions, monitor, effects); // warm-up / JIT
        pass.UpdateWindows(gpu.Context, windows);
        pass.Draw(gpu.Context, image.View, target.View, Size, Size, constants);

        // Worst case: windows move every frame, so regions are filled and uploaded every frame.
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            WindowConstants.Fill(ref windows, regions, monitor, effects);
            pass.UpdateWindows(gpu.Context, windows);
            pass.Draw(gpu.Context, image.View, target.View, Size, Size, constants);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
