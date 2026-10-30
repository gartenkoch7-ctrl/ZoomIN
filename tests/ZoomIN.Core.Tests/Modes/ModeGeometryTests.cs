using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Modes;

public sealed class ModeGeometryTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void Fullscreen_UsesWholeMonitor_WithoutFrame()
    {
        var frame = Compute(ZoomMode.Fullscreen, new PhysicalPoint(960, 540), 2);

        Assert.Equal(s_monitor, frame.Target);
        Assert.Equal(new PhysicalRect(480, 270, 960, 540), frame.Source);
        Assert.False(frame.HasFrame);
    }

    [Fact]
    public void Lens_IsCenteredOnCursor_WithDefaultSize()
    {
        var frame = Compute(ZoomMode.Lens, new PhysicalPoint(960, 540), 2);

        Assert.Equal(new PhysicalRect(576, 378, 768, 324), frame.Target);
        Assert.True(frame.HasFrame);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(1915, 1075)]
    [InlineData(960, 2)]
    public void Lens_StaysInsideMonitor(double x, double y)
    {
        var frame = Compute(ZoomMode.Lens, new PhysicalPoint(x, y), 4);

        AssertInside(frame.Target, s_monitor);
        AssertInside(frame.Source, frame.Target);
    }

    [Theory]
    [InlineData(960, 540, 2)]
    [InlineData(10, 10, 4)]
    [InlineData(1900, 1070, 8)]
    public void Lens_CursorPixel_IsDisplayedAtCursor(double x, double y, double zoom)
    {
        var frame = Compute(ZoomMode.Lens, new PhysicalPoint(x, y), zoom);

        // Map the cursor's desktop position from source to target: it must land on itself.
        var displayedX = frame.Target.X + ((x - frame.Source.X) * zoom);
        var displayedY = frame.Target.Y + ((y - frame.Source.Y) * zoom);

        Assert.Equal(x, displayedX, 6);
        Assert.Equal(y, displayedY, 6);
    }

    [Fact]
    public void Docked_Top_TargetIsTopStrip_SourceNeverInsideStrip()
    {
        var frame = Compute(ZoomMode.Docked, new PhysicalPoint(960, 100), 2);

        Assert.Equal(new PhysicalRect(0, 0, 1920, 432), frame.Target);
        Assert.True(frame.Source.Y >= 432);
        AssertInside(frame.Source, s_monitor);
    }

    [Theory]
    [InlineData(DockEdge.Bottom)]
    [InlineData(DockEdge.Left)]
    [InlineData(DockEdge.Right)]
    public void Docked_StripAndRest_CoverMonitorWithoutOverlap(DockEdge edge)
    {
        var dock = new DockLayout(edge, 0.25);

        var strip = ModeGeometry.DockRect(s_monitor, dock);
        var rest = ModeGeometry.RestRect(s_monitor, dock);

        Assert.Equal(s_monitor.Width * s_monitor.Height, (strip.Width * strip.Height) + (rest.Width * rest.Height), 6);
        AssertInside(strip, s_monitor);
        AssertInside(rest, s_monitor);
    }

    [Fact]
    public void Docked_AtZoomOne_SourceFitsIntoRest()
    {
        var frame = Compute(ZoomMode.Docked, new PhysicalPoint(960, 800), 1);

        AssertInside(frame.Source, ModeGeometry.RestRect(s_monitor, DockLayout.Default));
    }

    [Fact]
    public void LensSize_Resize_IsClamped()
    {
        var size = LensSize.Default.Resize(100, -100);

        Assert.Equal(LensSize.MaxFraction, size.WidthFraction);
        Assert.Equal(LensSize.MinFraction, size.HeightFraction);
    }

    private static ModeFrame Compute(ZoomMode mode, PhysicalPoint cursor, double zoom) =>
        ModeGeometry.Compute(mode, s_monitor, cursor, zoom, LensSize.Default, DockLayout.Default);

    private static void AssertInside(PhysicalRect inner, PhysicalRect outer)
    {
        Assert.True(inner.X >= outer.X - 1e-6 && inner.Right <= outer.Right + 1e-6, $"{inner} not inside {outer}");
        Assert.True(inner.Y >= outer.Y - 1e-6 && inner.Bottom <= outer.Bottom + 1e-6, $"{inner} not inside {outer}");
    }
}
