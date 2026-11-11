using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Own schemes (M18, C1–C3) on WARP: every kind, the colour replacement and its interplay.</summary>
public sealed class MagnifySchemeTests
{
    private const int Size = 64;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly Bgra s_white = new(255, 255, 255, 255);
    private static readonly Bgra s_black = new(0, 0, 0, 255);
    private static readonly Bgra s_yellow = new(0, 255, 255, 255);

    // Close to red, but clearly another colour: 16 steps less red, a little green.
    private static readonly Bgra s_nearRed = new(0, 8, 239, 255);

    private static readonly ColorReplacement s_redToYellow = new() { From = "#FF0000", To = "#FFFF00", Tolerance = 0 };

    [Fact]
    public void TwoColors_LightBecomesColor1_DarkColor2()
    {
        var scheme = Scheme(SchemeKind.TwoColors) with { Color1 = "#0D1B4C", Color2 = "#00E676" };
        var pixels = Render(Quarters, Monitor(scheme));

        AssertNear(new Bgra(76, 27, 13, 255), pixels.At(8, 8));      // white
        Assert.Equal(new Bgra(118, 230, 0, 255), pixels.At(40, 8));    // black
        AssertNear(Expected(scheme, TestImage.Red), pixels.At(8, 40));
    }

    [Fact]
    public void Tint_WhiteBecomesTheHue_BlackStays()
    {
        var scheme = Scheme(SchemeKind.Tint) with { Hue = "#FFC107" };
        var pixels = Render(Quarters, Monitor(scheme));

        AssertNear(new Bgra(7, 193, 255, 255), pixels.At(8, 8));
        Assert.Equal(s_black, pixels.At(40, 8));
        AssertNear(Expected(scheme, TestImage.Red), pixels.At(8, 40));
    }

    [Theory]
    [InlineData(ColorDeficiency.Protan)]
    [InlineData(ColorDeficiency.Deutan)]
    [InlineData(ColorDeficiency.Tritan)]
    public void Deficiency_KeepsWhiteAndBlack_AndMatchesTheMatrixOnColours(ColorDeficiency deficiency)
    {
        var scheme = Scheme(SchemeKind.Deficiency) with { Deficiency = deficiency, Strength = 70 };
        var pixels = Render(Quarters, Monitor(scheme));

        AssertNear(s_white, pixels.At(8, 8));
        Assert.Equal(s_black, pixels.At(40, 8));
        AssertNear(Expected(scheme, TestImage.Red), pixels.At(8, 40));
        AssertNear(Expected(scheme, TestImage.Blue), pixels.At(40, 40));
    }

    [Fact]
    public void Replacement_HitsTheTarget_ToleranceZeroLeavesNeighbours()
    {
        var scheme = Scheme(SchemeKind.ReplaceOnly) with { Replacements = [s_redToYellow] };
        var pixels = Render((x, _) => x < 16 ? TestImage.Red : x < 32 ? s_nearRed : x < 48 ? TestImage.Blue : s_white, Monitor(scheme));

        Assert.Equal(s_yellow, pixels.At(8, 8));
        Assert.Equal(s_nearRed, pixels.At(24, 8));
        Assert.Equal(TestImage.Blue, pixels.At(40, 8));
        Assert.Equal(s_white, pixels.At(56, 8));
    }

    [Fact]
    public void Replacement_ComesBeforeTheMatrix()
    {
        // Red → yellow, then grey by luma: yellow is much lighter than red.
        var scheme = Scheme(SchemeKind.TwoColors) with { Color1 = "#FFFFFF", Color2 = "#000000", Replacements = [s_redToYellow] };
        var pixels = Render((_, _) => TestImage.Red, Monitor(scheme));

        var grey = (int)Math.Round(255 * (0.2126 + 0.7152));
        AssertNear(new Bgra((byte)grey, (byte)grey, (byte)grey, 255), pixels.At(8, 8));
    }

    [Fact]
    public void WindowRule_ReplacesOnlyInItsWindow_MonitorKeepsItsOwnScheme()
    {
        var replacing = Scheme(SchemeKind.ReplaceOnly) with { Id = FilterId.Custom2, Replacements = [s_redToYellow] };
        var effects = new DisplayEffects(FilterSettings.Default, Smoothing.Pixel, PointerStyle.Default) { Schemes = [replacing] };
        var windows = new WindowRegions([new PhysicalRect(32, 0, 32, 64)], [WindowPalette.For(FilterId.Custom2)]);
        var pixels = Render((_, _) => TestImage.Red, effects, windows);

        Assert.Equal(TestImage.Red, pixels.At(8, 8));
        Assert.Equal(s_yellow, pixels.At(56, 8));
    }

    [Fact]
    public void WithSmartMonitor_TheSchemeWindowIsReplaced_TheRestInvertedSmartly()
    {
        var replacing = Scheme(SchemeKind.ReplaceOnly) with { Replacements = [s_redToYellow] };
        var effects = SmartInvertRender.SmartMonitor with { Schemes = [replacing] };
        var half = SmartInvertRender.Size / 2;
        var windows = new WindowRegions([new PhysicalRect(half, 0, half, SmartInvertRender.Size)], [WindowPalette.For(FilterId.Custom1)]);

        var pixels = SmartInvertRender.Render((x, _) => x < half ? s_white : TestImage.Red, effects, windows);

        Assert.True(SmartInvertRender.Luma(pixels.At(16, 64)) < 40, $"smart part {pixels.At(16, 64)}");
        Assert.Equal(s_yellow, pixels.At(100, 64));
    }

    [Fact]
    public void Constants_CarryCountsAndTheMonitorScheme()
    {
        var scheme = Scheme(SchemeKind.ReplaceOnly) with { Id = FilterId.Custom4, Replacements = [s_redToYellow, s_redToYellow] };
        var constants = default(WindowConstants);
        WindowConstants.Fill(ref constants, WindowRegions.Empty, s_monitor, Monitor(scheme));

        Assert.Equal(1u, constants.ReplaceActive);
        Assert.Equal(4u, constants.MonitorScheme);
        Assert.Equal(2u, constants.ReplaceCounts[3].X);
        Assert.Equal(0u, constants.ReplaceCounts[0].X);

        WindowConstants.Fill(ref constants, WindowRegions.Empty, s_monitor, DisplayEffects.Default);
        Assert.Equal(0u, constants.ReplaceActive);
        Assert.Equal(0u, constants.MonitorScheme);
        Assert.Equal(0u, constants.ReplaceCounts[3].X);
    }

    // Top left white, top right black, bottom left red, bottom right blue.
    private static Bgra Quarters(int x, int y) =>
        y < Size / 2 ? (x < Size / 2 ? s_white : s_black) : (x < Size / 2 ? TestImage.Red : TestImage.Blue);

    private static CustomScheme Scheme(SchemeKind kind) => new() { Id = FilterId.Custom1, Kind = kind };

    private static DisplayEffects Monitor(CustomScheme scheme) =>
        new(new FilterSettings(true, scheme.Id, 1, 0), Smoothing.Pixel, PointerStyle.Default) { Schemes = [scheme] };

    // What the CPU reference makes of a colour: replacements first, then the matrix.
    private static Bgra Expected(CustomScheme scheme, Bgra c)
    {
        var effects = Monitor(scheme);
        var (r, g, b) = effects.Replacements.Apply(CustomSchemes.SlotOf(scheme.Id), c.R / 255f, c.G / 255f, c.B / 255f);
        var (fr, fg, fb) = effects.Matrix.Apply(r, g, b);
        return new Bgra(Byte(fb), Byte(fg), Byte(fr), 255);
    }

    private static byte Byte(float v) => (byte)Math.Round(v * 255);

    private static void AssertNear(Bgra expected, Bgra actual)
    {
        Assert.True(
            Math.Abs(expected.R - actual.R) <= 2 && Math.Abs(expected.G - actual.G) <= 2 && Math.Abs(expected.B - actual.B) <= 2,
            $"expected {expected}, got {actual}");
    }

    private static PixelBuffer Render(Func<int, int, Bgra> image, DisplayEffects effects, WindowRegions? windows = null)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var texture = TestImage.Create(gpu.Device, Size, image);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);

        var constants = default(WindowConstants);
        WindowConstants.Fill(ref constants, windows ?? WindowRegions.Empty, s_monitor, effects);
        pass.UpdateWindows(gpu.Context, constants);
        pass.Draw(gpu.Context, texture.View, target.View, Size, Size,
            MagnifyConstants.Fullscreen(s_monitor, s_monitor, effects.Matrix, Smoothing.Pixel));
        return target.ReadBack(gpu.Device, gpu.Context);
    }
}
