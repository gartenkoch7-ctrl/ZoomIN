using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

/// <summary>Page and title bar counted apart from one shrunk picture (ADR-0023).</summary>
public sealed class BrightnessApartTests
{
    private const uint White = 0xFFFFFFFF;
    private const uint Black = 0xFF000000;
    private const int Width = 8;
    private const int Height = 40;
    private static readonly PhysicalRect s_frame = new(0, 0, 400, 400); // 10 px per row
    private static readonly PhysicalRect[] s_monitor = [new(0, 0, 1920, 1080)];

    [Fact]
    public void DarkTitleBar_OverALightPage_IsLightPage_DarkTitleBar()
    {
        var pixels = Picture(darkRows: 4); // 40 px title bar

        var (page, titleBar) = BrightnessMeter.IsLightApart(pixels, Width, Height, s_frame, 40, [], s_monitor, 90);

        Assert.True(page);
        Assert.False(titleBar);
    }

    [Fact]
    public void WithoutSplitting_TheSameDarkStripWouldTipALightPageAtAHighThreshold()
    {
        var pixels = Picture(darkRows: 4);

        Assert.False(BrightnessMeter.IsLight(pixels, Width, Height, s_frame, [], s_monitor, 95));
        Assert.True(BrightnessMeter.IsLightApart(pixels, Width, Height, s_frame, 40, [], s_monitor, 95).Page);
    }

    [Fact]
    public void RowCutByTheEdge_BelongsToNeither()
    {
        var pixels = Picture(darkRows: 4);
        pixels.AsSpan(4 * Width, Width).Fill(Black); // row 4 (40–50 px) is half title bar

        var (page, titleBar) = BrightnessMeter.IsLightApart(pixels, Width, Height, s_frame, 45, [], s_monitor, 100);

        Assert.True(page);
        Assert.False(titleBar);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void NoTitleBar_CountsTheWholeFrame(double bottom)
    {
        var (page, titleBar) = BrightnessMeter.IsLightApart(Picture(darkRows: 0), Width, Height, s_frame, bottom, [], s_monitor, 60);

        Assert.True(page);
        Assert.Null(titleBar);
    }

    [Fact]
    public void CoveredPage_IsUnknown_TitleBarStillCounted()
    {
        PhysicalRect[] cover = [new(0, 40, 400, 360)];

        var (page, titleBar) = BrightnessMeter.IsLightApart(Picture(darkRows: 4), Width, Height, s_frame, 40, cover, s_monitor, 60);

        Assert.Null(page);
        Assert.False(titleBar);
    }

    [Fact]
    public void MonitorAboveTheMainOne_NegativeY_SplitsAtTheRealTitleBar()
    {
        var frame = new PhysicalRect(0, -1000, 400, 400);
        PhysicalRect[] monitor = [new(0, -1080, 1920, 1080)];

        var (page, titleBar) = BrightnessMeter.IsLightApart(Picture(darkRows: 4), Width, Height, frame, -960, [], monitor, 90);

        Assert.True(page);
        Assert.False(titleBar);
    }

    private static uint[] Picture(int darkRows)
    {
        var pixels = new uint[Width * Height];
        pixels.AsSpan().Fill(White);
        pixels.AsSpan(0, darkRows * Width).Fill(Black);
        return pixels;
    }
}
