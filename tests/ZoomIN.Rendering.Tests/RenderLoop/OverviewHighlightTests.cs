using Vortice.Mathematics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Tests.Gpu;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>
/// M18 integration on WARP: caret shapes (E2) and show conditions (E1) while the overview (M17 B2)
/// is open. Both layers share one CursorPass, drawn in RenderSession's order: highlights, then frame.
/// </summary>
[Collection(MeterTests.Name)]
public sealed class OverviewHighlightTests
{
    private const int Size = 64;
    private const int Frames = 1_000;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly Bgra s_amber = new(8, 194, 255, 255);

    // Focus and caret lie outside the overview frame (16, 16, 32, 32), so their pixels add up.
    private static readonly TrackingSnapshot s_tracking = new(new PhysicalRect(2, 2, 10, 8), 1, new PhysicalRect(6, 50, 2, 10), 1);

    [Theory]
    [InlineData(CaretMarkerShape.Bar)]
    [InlineData(CaretMarkerShape.Wedges)]
    [InlineData(CaretMarkerShape.Frame)]
    [InlineData(CaretMarkerShape.Circle)]
    public void EveryCaretShape_AndTheOverviewFrame_AreBothDrawn(CaretMarkerShape shape)
    {
        var options = Options(shape, ShowCondition.Always);

        var frameOnly = Visible(Draw(options, Alphas(ShowCondition.Always), highlights: false, overview: true));
        var highlightsOnly = Visible(Draw(options, Alphas(ShowCondition.Always), highlights: true, overview: false));
        var pixels = Draw(options, Alphas(ShowCondition.Always), highlights: true, overview: true);
        var both = Visible(pixels);

        // Larger caret shapes may touch the frame, so the counts need not add up exactly.
        Assert.True(frameOnly > 0 && highlightsOnly > 0, $"frame {frameOnly} px, highlights {highlightsOnly} px");
        Assert.True(both > frameOnly && both > highlightsOnly, $"both {both} px, frame {frameOnly} px, highlights {highlightsOnly} px");
        Assert.Equal(s_amber, pixels.At(30, 17)); // the overview frame stays on top, unchanged
    }

    [Fact]
    public void CaretWaitingForTheHoldKey_StaysHidden_TheOverviewFrameStays()
    {
        var options = Options(CaretMarkerShape.Wedges, ShowCondition.HoldKey) with { FocusFrame = false };

        var frameOnly = Draw(options, Alphas(ShowCondition.HoldKey), highlights: false, overview: true);
        var both = Draw(options, Alphas(ShowCondition.HoldKey), highlights: true, overview: true);

        Assert.Equal(Visible(frameOnly), Visible(both));
        Assert.True(Visible(both) > 0);
    }

    [Fact]
    public void HighlightsAndOverviewFrame_DoNotAllocate()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var highlights = new HighlightLayer(pass, new HighlightMetrics(@"\\.\OVERVIEW_ALLOC"));
        var overview = new OverviewLayer(pass);
        var options = Options(CaretMarkerShape.Circle, ShowCondition.Always);
        var alphas = Alphas(ShowCondition.Always);
        var session = Session();
        var mouse = 0;

        void Frame()
        {
            // The mouse moves the overview frame every frame, as during a real overview.
            overview.Update(OneX(), session, s_monitor, new PhysicalPoint(30 + (mouse++ % 4), 32), 2, LensSize.Default, DockLayout.Default);
            highlights.Draw(gpu.Context, target.View, s_monitor, OneX(), s_tracking, options, alphas);
            overview.Draw(gpu.Context, target.View, s_monitor, OneX());
        }

        Frame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            Frame();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static TrackingOptions Options(CaretMarkerShape shape, ShowCondition caretShow) =>
        TrackingOptions.Default with { CaretStyle = CaretMarkerStyle.Default with { Shape = shape }, CaretShow = caretShow };

    private static MarkerAlphas Alphas(ShowCondition caret)
    {
        var alphas = new MarkerAlphas();
        alphas.Update(ShowCondition.Always, caret, ShowCondition.Always, default, 10_000, 1000);
        return alphas;
    }

    private static PixelBuffer Draw(TrackingOptions options, MarkerAlphas alphas, bool highlights, bool overview)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var highlightLayer = new HighlightLayer(pass, new HighlightMetrics(@"\\.\DISPLAY1"));
        var overviewLayer = new OverviewLayer(pass);
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        overviewLayer.Update(OneX(), overview ? Session() : null, s_monitor, new PhysicalPoint(32, 32), 2, LensSize.Default, DockLayout.Default);
        if (highlights)
        {
            highlightLayer.Draw(gpu.Context, target.View, s_monitor, OneX(), s_tracking, options, alphas);
        }

        overviewLayer.Draw(gpu.Context, target.View, s_monitor, OneX());
        return target.ReadBack(gpu.Device, gpu.Context);
    }

    private static int Visible(PixelBuffer pixels)
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

    private static ModeFrame OneX() =>
        ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, new PhysicalPoint(32, 32), 1, LensSize.Default, DockLayout.Default);

    private static OverviewSession Session() => new(1, @"\\.\DISPLAY1", ZoomMode.Fullscreen, 2, 0);
}
