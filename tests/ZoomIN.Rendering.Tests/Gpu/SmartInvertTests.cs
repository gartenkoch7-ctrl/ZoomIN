using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Smart inversion (ADR-0024) on WARP: light surroundings inverted, dark bars and photos kept.</summary>
public sealed class SmartInvertTests
{
    private const int Size = 128;

    // A dark bar (like the navigation bar of the Control Panel) with light text over a white page
    // with dark text; the page's text lines are 2 px high every 8 px.
    private const int BarBottom = 48;
    private static readonly Bgra s_white = new(255, 255, 255, 255);
    private static readonly Bgra s_black = new(0, 0, 0, 255);
    private static readonly Bgra s_bar = new(32, 32, 32, 255);
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly DisplayEffects s_smartMonitor =
        new(new FilterSettings(true, ColorFilter.InvertedSmart, 1, 0), Smoothing.Pixel, PointerStyle.Default);

    [Fact]
    public void LightPage_IsInverted_DarkBarStaysDark()
    {
        var pixels = Render(BarOverPage, s_smartMonitor);

        Assert.True(Luma(pixels.At(64, 16)) < 60, "the dark bar must stay dark");
        Assert.True(Luma(pixels.At(64, 100)) < 30, "white page must turn dark");
        Assert.True(Luma(pixels.At(64, 96)) > 200, "dark text on the page must turn light");
    }

    [Fact]
    public void Photo_StaysOriginal()
    {
        // Colourful, detailed light content: bright stripes of different hues.
        var pixels = Render(Photo, s_smartMonitor);

        Assert.Equal(Photo(64, 64), pixels.At(64, 64));
    }

    [Fact]
    public void Photo_IsInverted_WhenNotProtected()
    {
        var pixels = Render(Photo, s_smartMonitor with { ProtectPhotos = false });

        Assert.NotEqual(Photo(64, 64), pixels.At(64, 64));
    }

    [Fact]
    public void SmartWindow_OnlyInsideTheWindow()
    {
        var window = new WindowRegions([new PhysicalRect(0, 64, 64, 64)], [WindowPalette.Smart]);
        var pixels = Render(BarOverPage, DisplayEffects.Default, window);

        Assert.True(Luma(pixels.At(32, 100)) < 30, "white page inside the window turns dark");
        Assert.Equal(s_white, pixels.At(100, 100));
        Assert.Equal(s_bar, pixels.At(32, 16));
    }

    [Fact]
    public void Smoothing_ChangesFadeInOverFrames()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var dark = TestImage.Create(gpu.Device, Size, (_, _) => s_bar);
        using var light = TestImage.Create(gpu.Device, Size, (_, _) => s_white);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        using var ambient = new AmbientPass(gpu.Device, Size, Size);
        Upload(gpu, pass, s_smartMonitor, WindowRegions.Empty);

        ambient.Update(gpu.Context, dark.View, 1, protectPhotos: true);
        ambient.Update(gpu.Context, light.View, 0.25f, protectPhotos: true);
        Draw(gpu, pass, light, target, ambient);
        var halfway = Luma(target.ReadBack(gpu.Device, gpu.Context).At(64, 64));

        for (var i = 0; i < 20; i++)
        {
            ambient.Update(gpu.Context, light.View, 0.25f, protectPhotos: true);
        }

        Draw(gpu, pass, light, target, ambient);
        var settled = Luma(target.ReadBack(gpu.Device, gpu.Context).At(64, 64));

        Assert.InRange(halfway, 150, 220); // a quarter inverted: white only a bit darker
        Assert.True(settled < 10, $"after 20 frames the white page is inverted ({settled})");
    }

    [Fact]
    public void EdgeBetweenLightAndDark_StaysSharp_WithoutGreySeam()
    {
        // White page left, dark window right; the edge at x = 71 is not on a 16 px texel border.
        const int edge = 71;
        var pixels = Render((x, _) => x < edge ? s_white : s_bar, s_smartMonitor);

        for (var x = 0; x < Size; x++)
        {
            var luma = Luma(pixels.At(x, 64));
            if (x < edge - 1)
            {
                Assert.True(luma < 20, $"x={x}: white must be fully inverted, got luma {luma}");
            }
            else if (x > edge)
            {
                Assert.True(Math.Abs(luma - Luma(s_bar)) < 10, $"x={x}: dark must stay as it is, got luma {luma}");
            }
        }
    }

    [Fact]
    public void DarkTooltip_OnALightPage_StaysDark()
    {
        // Dark Windows theme: a 24 px high dark tooltip with light text over a white page.
        static Bgra Image(int x, int y) =>
            x is >= 20 and < 110 && y is >= 50 and < 74
                ? (y is 60 or 61 && x % 5 < 3 ? s_white : s_bar)
                : s_white;

        var pixels = Render(Image, s_smartMonitor);

        Assert.True(Luma(pixels.At(64, 54)) < 60, $"tooltip must stay dark, got {Luma(pixels.At(64, 54))}");
        Assert.True(Luma(pixels.At(64, 100)) < 20, "the page around it is inverted");
    }

    [Fact]
    public void DenseBoldText_OnWhite_IsStillInverted()
    {
        // Bold 3 px strokes every 6 px both ways: far more ink than normal text.
        var pixels = Render((x, y) => x % 6 < 3 && y % 8 < 4 ? s_black : s_white, s_smartMonitor);

        Assert.True(Luma(pixels.At(64, 66)) < 20, $"white between strokes must be inverted, got {Luma(pixels.At(64, 66))}");
        Assert.True(Luma(pixels.At(61, 64)) > 200, $"strokes must turn light, got {Luma(pixels.At(61, 64))}");
    }

    [Fact]
    public void ShaderBlock_MatchesAmbientPass()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "ZoomIN.Rendering", "Shaders", "Magnify.hlsl"));

        Assert.Contains($"#define WEIGHT_BLOCK {AmbientPass.Block}.0", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Monitor_NotSmart_NeedsNoWeights()
    {
        var pixels = Render(BarOverPage, DisplayEffects.Default);

        Assert.Equal(s_white, pixels.At(64, 100));
    }

    private static Bgra BarOverPage(int x, int y) =>
        y < BarBottom
            ? (y % 16 is 7 or 8 && x % 4 < 2 ? s_white : s_bar)
            : (y % 8 is 0 or 1 && x % 16 < 12 ? s_black : s_white);

    // Light, saturated patches of changing hue, 8 px each. Shifted by 2 px so the samples of the
    // ambient pass (2 × 2 means every 8 px) fall inside a patch instead of averaging four hues.
    private static Bgra Photo(int x, int y) => ((((x + 2) / 8) + ((y + 2) / 8)) % 3) switch
    {
        0 => new Bgra(80, 230, 255, 255),
        1 => new Bgra(255, 200, 90, 255),
        _ => new Bgra(140, 255, 160, 255),
    };

    private static int Luma(Bgra c) => (int)((0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B));

    private static PixelBuffer Render(Func<int, int, Bgra> image, DisplayEffects effects, WindowRegions? windows = null)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var texture = TestImage.Create(gpu.Device, Size, image);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        using var ambient = new AmbientPass(gpu.Device, Size, Size);
        Upload(gpu, pass, effects, windows ?? WindowRegions.Empty);

        ambient.Update(gpu.Context, texture.View, 1, effects.ProtectPhotos);
        pass.Draw(gpu.Context, texture.View, target.View, Size, Size,
            MagnifyConstants.Fullscreen(s_monitor, s_monitor, effects.Matrix, Smoothing.Pixel, effects.IsSmart), ambient.Weights);
        return target.ReadBack(gpu.Device, gpu.Context);
    }

    private static void Upload(GpuDevice gpu, MagnifyPass pass, DisplayEffects effects, WindowRegions windows)
    {
        var constants = default(WindowConstants);
        WindowConstants.Fill(ref constants, windows, s_monitor, effects);
        pass.UpdateWindows(gpu.Context, constants);
    }

    private static void Draw(GpuDevice gpu, MagnifyPass pass, TestImage image, RenderTarget target, AmbientPass ambient) =>
        pass.Draw(gpu.Context, image.View, target.View, Size, Size,
            MagnifyConstants.Fullscreen(s_monitor, s_monitor, s_smartMonitor.Matrix, Smoothing.Pixel, smart: true), ambient.Weights);
}
