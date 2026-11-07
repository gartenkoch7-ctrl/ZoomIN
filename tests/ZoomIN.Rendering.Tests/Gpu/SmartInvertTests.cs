using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using static ZoomIN.Rendering.Tests.Gpu.SmartInvertRender;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Smart inversion (ADR-0024) on WARP: light surroundings inverted, dark bars and photos kept.</summary>
public sealed class SmartInvertTests
{
    // A dark bar (like the navigation bar of the Control Panel) with light text over a white page
    // with dark text; the page's text lines are 2 px high every 8 px.
    private const int BarBottom = 48;

    [Fact]
    public void LightPage_IsInverted_DarkBarStaysDark()
    {
        var pixels = Render(BarOverPage, SmartMonitor);

        Assert.True(Luma(pixels.At(64, 16)) < 60, "the dark bar must stay dark");
        Assert.True(Luma(pixels.At(64, 100)) < 30, "white page must turn dark");
        Assert.True(Luma(pixels.At(64, 96)) > 200, "dark text on the page must turn light");
    }

    [Fact]
    public void Photo_StaysOriginal()
    {
        var pixels = Render(Photo, SmartMonitor);

        Assert.Equal(Photo(64, 64), pixels.At(64, 64));
        Assert.Equal(Photo(20, 100), pixels.At(20, 100));
    }

    [Fact]
    public void BurntOutWhiteInsideAPhoto_StaysOriginal()
    {
        // Pure white 40 × 40 in the middle of a photo: paper on its own, a hole in the photo by its surroundings.
        static Bgra Image(int x, int y) => x is >= 44 and < 84 && y is >= 44 and < 84 ? White : Photo(x, y);

        var pixels = Render(Image, SmartMonitor);

        Assert.True(Luma(pixels.At(64, 64)) > 240, $"the white area must stay white, got {Luma(pixels.At(64, 64))}");
    }

    [Fact]
    public void SmartWindow_OnlyInsideTheWindow()
    {
        var window = new WindowRegions([new PhysicalRect(0, 64, 64, 64)], [WindowPalette.Smart]);
        var pixels = Render(BarOverPage, DisplayEffects.Default, window);

        Assert.True(Luma(pixels.At(32, 100)) < 30, "white page inside the window turns dark");
        Assert.Equal(White, pixels.At(100, 100));
        Assert.Equal(Bar, pixels.At(32, 16));
    }

    [Fact]
    public void Smoothing_BorderlineChangeFadesInOverFrames()
    {
        // Mid grey just below and just above "light": a similar background that flips – smoothed.
        var (halfway, settled) = Fade(new Bgra(150, 150, 150, 255), new Bgra(182, 182, 182, 255));

        Assert.InRange(halfway, 140, 172); // a quarter inverted: only a bit darker than 182
        Assert.True(settled < 90, $"after 20 frames the grey is inverted ({settled})");
    }

    [Fact]
    public void Smoothing_ClearChangeCountsAtOnce()
    {
        // Dark content replaced by a white page (scrolling, a window moved): no grey ghost.
        var (first, _) = Fade(Bar, White);

        Assert.True(first < 10, $"the white page is inverted in the first frame ({first})");
    }

    private static (int First, int Settled) Fade(Bgra before, Bgra after)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var old = TestImage.Create(gpu.Device, Size, (_, _) => before);
        using var next = TestImage.Create(gpu.Device, Size, (_, _) => after);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        using var ambient = new AmbientPass(gpu.Device, Size, Size);
        Upload(gpu, pass, SmartMonitor, WindowRegions.Empty);

        ambient.Update(gpu.Context, old.View, 1, protectPhotos: true);
        ambient.Update(gpu.Context, next.View, 0.25f, protectPhotos: true);
        Draw(gpu, pass, next, target, ambient);
        var first = Luma(target.ReadBack(gpu.Device, gpu.Context).At(64, 64));

        for (var i = 0; i < 20; i++)
        {
            ambient.Update(gpu.Context, next.View, 0.25f, protectPhotos: true);
        }

        Draw(gpu, pass, next, target, ambient);
        return (first, Luma(target.ReadBack(gpu.Device, gpu.Context).At(64, 64)));
    }

    [Fact]
    public void EdgeBetweenLightAndDark_StaysSharp_WithoutGreySeam()
    {
        // White page left, dark window right; the edge at x = 71 is not on a 16 px texel border.
        const int edge = 71;
        var pixels = Render((x, _) => x < edge ? White : Bar, SmartMonitor);

        for (var x = 0; x < Size; x++)
        {
            var luma = Luma(pixels.At(x, 64));
            if (x < edge - 1)
            {
                Assert.True(luma < 20, $"x={x}: white must be fully inverted, got luma {luma}");
            }
            else if (x > edge)
            {
                Assert.True(Math.Abs(luma - Luma(Bar)) < 10, $"x={x}: dark must stay as it is, got luma {luma}");
            }
        }
    }

    [Fact]
    public void DarkTooltip_OnALightPage_StaysDark()
    {
        // Dark Windows theme: a 24 px high dark tooltip with light text over a white page.
        static Bgra Image(int x, int y) =>
            x is >= 20 and < 110 && y is >= 50 and < 74
                ? (y is 60 or 61 && x % 5 < 3 ? White : Bar)
                : White;

        var pixels = Render(Image, SmartMonitor);

        Assert.True(Luma(pixels.At(64, 54)) < 60, $"tooltip must stay dark, got {Luma(pixels.At(64, 54))}");
        Assert.True(Luma(pixels.At(64, 100)) < 20, "the page around it is inverted");
    }

    [Fact]
    public void DenseBoldText_OnWhite_IsStillInverted()
    {
        // Bold 3 px strokes every 6 px both ways: far more ink than normal text.
        var pixels = Render((x, y) => x % 6 < 3 && y % 8 < 4 ? Black : White, SmartMonitor);

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

        Assert.Equal(White, pixels.At(64, 100));
    }

    private static Bgra BarOverPage(int x, int y) =>
        y < BarBottom
            ? (y % 16 is 7 or 8 && x % 4 < 2 ? White : Bar)
            : (y % 8 is 0 or 1 && x % 16 < 12 ? Black : White);

    // Photo-like: smooth colour gradients plus fine noise, no flat area anywhere (like a real photo).
    private static Bgra Photo(int x, int y)
    {
        var noise = ((x * 7919) ^ (y * 104729)) % 21 - 10;
        static byte Clamp(double v) => (byte)Math.Clamp(v, 0, 255);
        return new Bgra(
            Clamp(150 + (70 * Math.Cos(x * 0.11)) + noise),
            Clamp(170 + (60 * Math.Sin(y * 0.17)) + noise),
            Clamp(180 + (75 * Math.Sin((x * 0.21) + (y * 0.13))) + noise),
            255);
    }

    private static void Draw(GpuDevice gpu, MagnifyPass pass, TestImage image, RenderTarget target, AmbientPass ambient) =>
        pass.Draw(gpu.Context, image.View, target.View, Size, Size,
            MagnifyConstants.Fullscreen(Screen, Screen, SmartMonitor.Matrix, Smoothing.Pixel, smart: true), ambient.Weights);
}
