using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Viewport;

public sealed class ViewportCalculatorTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void ZoomOne_ReturnsWholeMonitor()
    {
        var source = ViewportCalculator.SourceRect(s_monitor, new PhysicalPoint(500, 300), 1.0);

        Assert.Equal(s_monitor, source);
    }

    [Fact]
    public void ZoomBelowOne_IsTreatedAsOne()
    {
        var source = ViewportCalculator.SourceRect(s_monitor, new PhysicalPoint(500, 300), 0.5);

        Assert.Equal(s_monitor, source);
    }

    [Fact]
    public void CursorInCenter_ZoomTwo_ShowsCenterQuarter()
    {
        var source = ViewportCalculator.SourceRect(s_monitor, new PhysicalPoint(960, 540), 2.0);

        Assert.Equal(new PhysicalRect(480, 270, 960, 540), source);
    }

    [Fact]
    public void CursorTopLeft_ShowsTopLeftCorner()
    {
        var source = ViewportCalculator.SourceRect(s_monitor, new PhysicalPoint(0, 0), 4.0);

        Assert.Equal(new PhysicalRect(0, 0, 480, 270), source);
    }

    [Fact]
    public void CursorBottomRight_ShowsBottomRightCorner()
    {
        var source = ViewportCalculator.SourceRect(s_monitor, new PhysicalPoint(1920, 1080), 4.0);

        Assert.Equal(1920, source.Right, 9);
        Assert.Equal(1080, source.Bottom, 9);
    }

    [Theory]
    [InlineData(0, 0, 1.0)]
    [InlineData(123, 456, 2.0)]
    [InlineData(1919, 1, 16.0)]
    [InlineData(700, 1079, 3.7)]
    public void CursorPixel_IsDisplayedAtCursorPosition(double x, double y, double zoom)
    {
        var source = ViewportCalculator.SourceRect(s_monitor, new PhysicalPoint(x, y), zoom);

        // Map the cursor's desktop position through the zoom: it must land on itself (fixpoint).
        var displayedX = (x - source.X) * zoom;
        var displayedY = (y - source.Y) * zoom;

        Assert.Equal(x, displayedX, 9);
        Assert.Equal(y, displayedY, 9);
    }

    [Theory]
    [InlineData(-500, -500)]
    [InlineData(5000, 5000)]
    [InlineData(960, 540)]
    public void SourceRect_AlwaysInsideMonitor(double x, double y)
    {
        var monitor = new PhysicalRect(-1920, 200, 1920, 1080);

        var source = ViewportCalculator.SourceRect(monitor, new PhysicalPoint(x, y), 8.0);

        Assert.True(source.X >= monitor.X - 1e-9 && source.Right <= monitor.Right + 1e-9);
        Assert.True(source.Y >= monitor.Y - 1e-9 && source.Bottom <= monitor.Bottom + 1e-9);
    }

    [Fact]
    public void Contains_UsesHalfOpenBounds()
    {
        var rect = new PhysicalRect(0, 0, 10, 10);

        Assert.True(rect.Contains(new PhysicalPoint(0, 0)));
        Assert.False(rect.Contains(new PhysicalPoint(10, 5)));
    }
}
