using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Filter per window (ADR-0020) on WARP, with the red/blue/green test image.</summary>
public sealed class MagnifyWindowTests
{
    private const int Size = 64;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly Bgra s_cyan = new(255, 255, 0, 255);
    private static readonly Bgra s_yellow = new(0, 255, 255, 255);
    private static readonly byte s_inverted = WindowPalette.For(FilterId.Inverted);

    [Fact]
    public void Palette_MatchesTheShader()
    {
        Assert.Equal(WindowConstants.PaletteRows, WindowPalette.Count * 3);
    }

    [Fact]
    public void OnlyTheWindowIsFiltered()
    {
        // A "window" over the blue right half.
        var pixels = Render(Regions((new PhysicalRect(32, 0, 32, 64), s_inverted)), DisplayEffects.Default);

        Assert.Equal(TestImage.Red, pixels.At(24, 40));
        Assert.Equal(s_yellow, pixels.At(56, 40));
    }

    [Fact]
    public void WindowAbove_HidesTheOneBelow()
    {
        // Front: original colours over x 40–64; behind: inverted over the whole right half.
        var pixels = Render(
            Regions((new PhysicalRect(40, 0, 24, 64), WindowPalette.Original), (new PhysicalRect(32, 0, 32, 64), s_inverted)),
            DisplayEffects.Default);

        Assert.Equal(s_yellow, pixels.At(36, 40));
        Assert.Equal(TestImage.Blue, pixels.At(56, 40));
    }

    [Fact]
    public void InvertedMonitor_InvertedWindowIsNotInvertedTwice_OriginalWindowStaysOriginal()
    {
        var invertedMonitor = new DisplayEffects(new FilterSettings(true, FilterId.Inverted, 1, 0), Smoothing.Pixel, PointerStyle.Default);
        var pixels = Render(
            Regions((new PhysicalRect(32, 0, 16, 64), s_inverted), (new PhysicalRect(48, 0, 16, 64), WindowPalette.Original)),
            invertedMonitor);

        Assert.Equal(s_cyan, pixels.At(24, 40));   // outside every window: monitor filter
        Assert.Equal(s_yellow, pixels.At(40, 40)); // own inversion only
        Assert.Equal(TestImage.Blue, pixels.At(56, 40));
    }

    [Fact]
    public void Zoomed_TheFilterFollowsTheSourcePixel()
    {
        // 4× onto the red/blue edge: the window over the blue half fills the right half of the view.
        var source = new PhysicalRect(24, 24, 16, 16);
        var pixels = Render(Regions((new PhysicalRect(32, 0, 32, 64), s_inverted)), DisplayEffects.Default, source);

        Assert.Equal(TestImage.Red, pixels.At(8, 40));
        Assert.Equal(s_yellow, pixels.At(56, 40));
    }

    [Fact]
    public void RegionsOnAnotherMonitor_AreIgnored()
    {
        var pixels = Render(Regions((new PhysicalRect(1000, 0, 500, 500), s_inverted)), DisplayEffects.Default);

        Assert.Equal(TestImage.Red, pixels.At(24, 40));
        Assert.Equal(TestImage.Blue, pixels.At(56, 40));
    }

    private static WindowRegions Regions(params (PhysicalRect Rect, byte Palette)[] windows) =>
        new(windows.Select(w => w.Rect).ToArray(), windows.Select(w => w.Palette).ToArray());

    private static PixelBuffer Render(WindowRegions regions, DisplayEffects effects, PhysicalRect? source = null)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);

        var windows = default(WindowConstants);
        WindowConstants.Fill(ref windows, regions, s_monitor, effects);
        pass.UpdateWindows(gpu.Context, windows);
        pass.Draw(gpu.Context, image.View, target.View, Size, Size,
            MagnifyConstants.Fullscreen(source ?? s_monitor, s_monitor, effects.Matrix, Smoothing.Pixel));

        return target.ReadBack(gpu.Device, gpu.Context);
    }
}
