using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>Size, level and place of an island always fit its monitor (M19 B1).</summary>
public sealed class IslandRulesTests
{
    private static readonly PhysicalRect s_monitor = IslandMonitors.FirstBounds;

    [Fact]
    public void Create_GrowsAroundTheSourceCentre()
    {
        var island = IslandRules.Create(1, IslandMonitors.First, new PhysicalRect(900, 500, 100, 40), s_monitor, 3);

        Assert.Equal(new PhysicalRect(900, 500, 100, 40), island.Source);
        Assert.Equal(new PhysicalRect(800, 460, 300, 120), island.Target);
        Assert.Equal(3, island.Level);
    }

    [Fact]
    public void Create_ClockInTheCorner_StaysInTheCorner()
    {
        var island = IslandRules.Create(1, IslandMonitors.First, new PhysicalRect(1820, 1040, 100, 40), s_monitor, 3);

        Assert.Equal(new PhysicalRect(1620, 960, 300, 120), island.Target);
    }

    [Fact]
    public void Fit_SourceHalfOffTheMonitor_KeepsTheVisiblePart()
    {
        var island = Fit(new PhysicalRect(1900, -10, 100, 50), new PhysicalPoint(0, 0), 2);

        Assert.Equal(new PhysicalRect(1900, 0, 20, 40), island.Source);
    }

    [Fact]
    public void Fit_TinySource_GrowsToTheMinimum()
    {
        var island = Fit(new PhysicalRect(10, 10, 3, 2), new PhysicalPoint(10, 10), 2);

        Assert.Equal(IslandRules.MinSize, island.Source.Width);
        Assert.Equal(IslandRules.MinSize, island.Source.Height);
    }

    [Theory]
    [InlineData(0.2, ZoomLimits.Min)]
    [InlineData(40, ZoomLimits.Max)]
    [InlineData(double.NaN, ZoomLimits.DefaultLevel)]
    [InlineData(double.PositiveInfinity, ZoomLimits.DefaultLevel)]
    public void Fit_ClampsTheLevel(double level, double expected)
    {
        var island = Fit(new PhysicalRect(100, 100, 50, 50), new PhysicalPoint(100, 100), level);

        Assert.Equal(expected, island.Level);
    }

    [Fact]
    public void Fit_PictureLargerThanTheMonitor_LowersTheLevel()
    {
        var island = Fit(new PhysicalRect(0, 0, 960, 100), new PhysicalPoint(0, 0), 4);

        Assert.Equal(2, island.Level);
        Assert.Equal(new PhysicalRect(0, 0, 1920, 200), island.Target);
    }

    [Fact]
    public void Fit_LevelCappedToTheMonitor_NeverThrows_AndStaysInside()
    {
        // 147 · (1280 / 147) is one rounding step above 1280: the clamp bounds must not cross.
        var monitor = new PhysicalRect(0, 0, 1280, 1024);
        for (var width = 17; width < 1280; width += 1)
        {
            var island = IslandRules.Fit(new Island(1, "M", new PhysicalRect(0, 0, width, 20), new PhysicalPoint(50, 50), ZoomLimits.Max), monitor);

            Assert.True(island.Target.Right <= monitor.Right + 0.5, $"width {width}");
        }
    }

    [Fact]
    public void FitTo_FitsKnownMonitors_KeepsOthers_AndIsTheSameSetIfNothingMoved()
    {
        var set = IslandSet.Empty
            .Add(new Island(0, IslandMonitors.First, new PhysicalRect(100, 100, 50, 50), new PhysicalPoint(5000, 100), 2))!
            .Add(new Island(0, @"\\.\GONE", new PhysicalRect(100, 100, 50, 50), new PhysicalPoint(5000, 100), 2))!;

        var fitted = set.FitTo(new IslandMonitors().BoundsOf);

        Assert.Equal(1820, fitted[0].TargetOrigin.X);
        Assert.Equal(5000, fitted[1].TargetOrigin.X);
        Assert.Same(fitted, fitted.FitTo(new IslandMonitors().BoundsOf));
    }

    [Fact]
    public void Fit_PictureOffTheMonitor_IsPushedBack()
    {
        var island = Fit(new PhysicalRect(100, 100, 100, 50), new PhysicalPoint(1900, -300), 2);

        Assert.Equal(new PhysicalRect(1720, 0, 200, 100), island.Target);
    }

    [Fact]
    public void Fit_NonFiniteValues_FallBackSafely()
    {
        var island = Fit(new PhysicalRect(double.NaN, double.NaN, double.NaN, -5), new PhysicalPoint(double.NaN, double.PositiveInfinity), 2);

        Assert.True(IsInside(island.Source, s_monitor));
        Assert.True(IsInside(island.Target, s_monitor));
    }

    [Fact]
    public void Fit_OnTheSecondMonitor_UsesItsCoordinates()
    {
        var island = IslandRules.Fit(
            new Island(1, IslandMonitors.Second, new PhysicalRect(100, 100, 50, 50), new PhysicalPoint(0, 0), 2), IslandMonitors.SecondBounds);

        Assert.True(IsInside(island.Source, IslandMonitors.SecondBounds));
        Assert.True(IsInside(island.Target, IslandMonitors.SecondBounds));
    }

    [Fact]
    public void WithLevel_KeepsThePictureCentre()
    {
        var island = IslandRules.Create(1, IslandMonitors.First, new PhysicalRect(900, 500, 100, 40), s_monitor, 2);

        var higher = IslandRules.WithLevel(island, 4, s_monitor);

        Assert.Equal(new PhysicalRect(750, 440, 400, 160), higher.Target);
        Assert.Equal(island.Source, higher.Source);
    }

    [Theory]
    [InlineData(15, 40, false)]
    [InlineData(16, 16, true)]
    [InlineData(40, 15, false)]
    public void IsLargeEnough_NeedsTheMinimumOnBothSides(double width, double height, bool expected) =>
        Assert.Equal(expected, IslandRules.IsLargeEnough(new PhysicalRect(0, 0, width, height)));

    private static Island Fit(PhysicalRect source, PhysicalPoint origin, double level) =>
        IslandRules.Fit(new Island(1, IslandMonitors.First, source, origin, level), s_monitor);

    private static bool IsInside(PhysicalRect rect, PhysicalRect monitor) =>
        rect.X >= monitor.X && rect.Y >= monitor.Y && rect.Right <= monitor.Right && rect.Bottom <= monitor.Bottom && rect.Width > 0 && rect.Height > 0;
}
