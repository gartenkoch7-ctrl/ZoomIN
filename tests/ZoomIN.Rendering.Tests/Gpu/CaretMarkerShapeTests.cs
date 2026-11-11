using Vortice.Mathematics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Tests.RenderLoop;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Caret marker shapes (E2) of HighlightLayer and Cursor.hlsl on WARP.</summary>
/// <remarks>Draws record zoomin.highlight.caret_frames, which RenderLayersAllocationTests counts.</remarks>
[Collection(MeterTests.Name)]
public sealed class CaretMarkerShapeTests
{
    private const int Size = 64;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);

    // 1×: the caret is drawn where it is. Medium at 1× = 4 px lines, wedges 11.2 px high.
    private static readonly PhysicalRect s_caret = new(30, 24, 2, 16);

    [Fact]
    public void Wedge_TipDown_FillsTheTopAndLeavesTheLowerCornersEmpty()
    {
        var pixels = DrawWedge(pointsUp: false);

        Assert.Equal(255, pixels.At(16, 4).A);
        Assert.Equal(0, pixels.At(2, 28).A);
        Assert.Equal(0, pixels.At(29, 28).A);
    }

    [Fact]
    public void Wedge_TipUp_IsMirrored()
    {
        var pixels = DrawWedge(pointsUp: true);

        Assert.Equal(255, pixels.At(16, 27).A);
        Assert.Equal(0, pixels.At(2, 3).A);
        Assert.Equal(0, pixels.At(29, 3).A);
    }

    [Fact]
    public void Wedges_SitAboveAndBelowTheCaret_NotOnIt()
    {
        var pixels = Draw(CaretMarkerShape.Wedges, s_caret);

        Assert.True(pixels.At(31, 17).A > 200, "wedge above the caret");
        Assert.True(pixels.At(31, 46).A > 200, "wedge below the caret");
        Assert.Equal(0, pixels.At(31, 32).A);
        Assert.Equal(0, pixels.At(24, 22).A); // beside the tip of the upper wedge
    }

    [Fact]
    public void Circle_IsARingAroundTheCaret()
    {
        // Radius = half the caret height + 2 lines = 16, ring 4 px wide, centre (31, 32).
        var pixels = Draw(CaretMarkerShape.Circle, s_caret);

        Assert.True(pixels.At(45, 32).A > 200, "ring right of the caret");
        Assert.True(pixels.At(31, 18).A > 200, "ring above the caret");
        Assert.Equal(0, pixels.At(31, 32).A);
        Assert.Equal(0, pixels.At(38, 32).A);
    }

    [Fact]
    public void Frame_IsHollowAroundTheCharacterAfterTheCaret()
    {
        // Thin caret: the character starts at its centre (31) and is height × 0.5 = 8 wide; pad 6.
        var pixels = Draw(CaretMarkerShape.Frame, s_caret);

        Assert.True(pixels.At(26, 32).A > 200, "left edge");
        Assert.True(pixels.At(43, 32).A > 200, "right edge");
        Assert.Equal(0, pixels.At(35, 32).A);
    }

    [Fact]
    public void Bar_KeepsTheLookFromBeforeE2()
    {
        // Magenta at 85 %, 4 px wide, centred on the caret.
        var pixels = Draw(CaretMarkerShape.Bar, s_caret, CaretMarkerStyle.Default.Color);

        Assert.InRange(pixels.At(29, 30).R, 215, 218);
        Assert.Equal(0, pixels.At(29, 30).G);
        Assert.InRange(pixels.At(29, 30).A, 215, 218);
        Assert.Equal(0, pixels.At(27, 30).A);
    }

    [Fact]
    public void ChosenColour_IsDrawnPremultipliedAt85Percent()
    {
        var pixels = Draw(CaretMarkerShape.Bar, s_caret, RgbColor.Yellow);

        // Yellow (255, 214, 0) × 0.85.
        Assert.InRange(pixels.At(31, 30).R, 215, 218);
        Assert.InRange(pixels.At(31, 30).G, 180, 183);
        Assert.Equal(0, pixels.At(31, 30).B);
    }

    [Fact]
    public void WedgeAbove_AtTheTopEdge_StaysWholeInsideTheMonitor()
    {
        var pixels = Draw(CaretMarkerShape.Wedges, s_caret with { Y = 0 });

        // Moved down into the monitor: its wide edge is now the first row.
        Assert.True(pixels.At(31, 1).A > 200);
    }

    [Fact]
    public void EstimatedCaret_DrawsNothing()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var layer = new HighlightLayer(pass, new HighlightMetrics(@"\\.\DISPLAY1"));
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        layer.Draw(gpu.Context, target.View, s_monitor, Frame(), new TrackingSnapshot(null, 0, s_caret, 1, CaretEstimated: true),
            TrackingOptions.Default);
        var pixels = target.ReadBack(gpu.Device, gpu.Context);

        Assert.Equal(0, pixels.At(31, 30).A);
    }

    private static ModeFrame Frame() =>
        ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, new PhysicalPoint(32, 32), 1, LensSize.Default, DockLayout.Default);

    private static PixelBuffer Draw(CaretMarkerShape shape, PhysicalRect caret, RgbColor? color = null)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var layer = new HighlightLayer(pass, new HighlightMetrics(@"\\.\DISPLAY1"));
        var style = new CaretMarkerStyle(shape, color ?? RgbColor.White, CaretMarkerThickness.Medium);
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        layer.Draw(gpu.Context, target.View, s_monitor, Frame(), new TrackingSnapshot(null, 0, caret, 1),
            TrackingOptions.Default with { CaretStyle = style });
        return target.ReadBack(gpu.Device, gpu.Context);
    }

    private static PixelBuffer DrawWedge(bool pointsUp)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, 32);
        using var pass = new CursorPass(gpu.Device);
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        pass.DrawWedge(gpu.Context, target.View, 0, 0, 32, 32, CursorConstants.ForWedge(pointsUp, new FrameColor(1, 1, 1, 1)));
        pass.End(gpu.Context);
        return target.ReadBack(gpu.Device, gpu.Context);
    }
}
