using Vortice.Mathematics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Tests.RenderLoop;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Highlights hidden by their show condition draw no pixel; halfway through a fade, half (E1, WARP).</summary>
[Collection(MeterTests.Name)]
public sealed class MarkerVisibilityRenderTests
{
    private const int Size = 64;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly TrackingSnapshot s_tracking = new(new PhysicalRect(10, 10, 20, 12), 1, new PhysicalRect(40, 30, 2, 16), 1);

    [Fact]
    public void HiddenCaretAndFocusFrame_DrawNoPixel()
    {
        var alphas = Alphas(ShowCondition.HoldKey, ShowCondition.HoldKey);

        var pixels = DrawHighlights(alphas);

        Assert.Equal(0, CountVisible(pixels));
    }

    [Fact]
    public void ShownCaretAndFocusFrame_Draw()
    {
        var pixels = DrawHighlights(Alphas(ShowCondition.Always, ShowCondition.Always));

        Assert.True(pixels.At(41, 36).A > 200, "caret bar");
        Assert.True(pixels.At(6, 16).A > 200, "focus frame edge");
    }

    [Fact]
    public void OnlyTheCaretWaitsForTheKey_TheFocusFrameStays()
    {
        var pixels = DrawHighlights(Alphas(ShowCondition.HoldKey, ShowCondition.Always));

        Assert.Equal(0, pixels.At(41, 36).A);
        Assert.True(pixels.At(6, 16).A > 200);
    }

    [Fact]
    public void HalfwayThroughTheFade_TheCaretIsHalfTransparent()
    {
        var alphas = new MarkerAlphas();
        alphas.Update(ShowCondition.Always, ShowCondition.Idle, ShowCondition.Always, default, 10_000, 1000);
        alphas.Update(ShowCondition.Always, ShowCondition.Idle, ShowCondition.Always, new(10_000, 0), 10_050, 1000);

        var pixels = DrawHighlights(alphas);

        // 85 % caret opacity × 0.5 fade ≈ 108.
        Assert.InRange(pixels.At(41, 36).A, 105, 112);
    }

    [Fact]
    public void HiddenCrosshair_DrawsNoLine()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var layer = new CursorLayer(pass, new NoShapes(), new NoSystemCursor());
        var style = PointerStyle.Default with { CrosshairEnabled = true, CrosshairAlways = true, CrosshairOpacity = 1 };
        var effects = new DisplayEffects(FilterSettings.Default, Smoothing.Text, style);
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        layer.Draw(gpu.Context, target.View, s_monitor, Frame(), ZoomMode.Fullscreen, 1, new PhysicalPoint(32, 32), effects, 0,
            hidden: false, crosshairAlpha: 0);
        var hidden = target.ReadBack(gpu.Device, gpu.Context);
        layer.Draw(gpu.Context, target.View, s_monitor, Frame(), ZoomMode.Fullscreen, 1, new PhysicalPoint(32, 32), effects, 0,
            hidden: false, crosshairAlpha: 1);
        var shown = target.ReadBack(gpu.Device, gpu.Context);

        Assert.Equal(0, CountVisible(hidden));
        Assert.True(shown.At(5, 32).A > 200);
    }

    private static MarkerAlphas Alphas(ShowCondition caret, ShowCondition focus)
    {
        var alphas = new MarkerAlphas();
        alphas.Update(ShowCondition.Always, caret, focus, default, 10_000, 1000);
        return alphas;
    }

    private static ModeFrame Frame() =>
        ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, new PhysicalPoint(32, 32), 1, LensSize.Default, DockLayout.Default);

    private static PixelBuffer DrawHighlights(MarkerAlphas alphas)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var layer = new HighlightLayer(pass, new HighlightMetrics(@"\\.\DISPLAY1"));
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        layer.Draw(gpu.Context, target.View, s_monitor, Frame(), s_tracking, TrackingOptions.Default, alphas);
        return target.ReadBack(gpu.Device, gpu.Context);
    }

    private static int CountVisible(PixelBuffer pixels)
    {
        var count = 0;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                count += pixels.At(x, y).A > 0 ? 1 : 0;
            }
        }

        return count;
    }

    private sealed class NoShapes : ICursorShapeSource
    {
        public bool TryGetCurrent(out nint handle)
        {
            handle = 0;
            return false;
        }

        public nint ArrowHandle() => 0;

        public CursorShape? Load(nint handle) => null;

        public CursorShape? LoadStandard(nint handle, double scale) => null;
    }

    private sealed class NoSystemCursor : ISystemCursor
    {
        public void SetVisible(bool visible)
        {
        }

        public void Dispose()
        {
        }
    }
}
