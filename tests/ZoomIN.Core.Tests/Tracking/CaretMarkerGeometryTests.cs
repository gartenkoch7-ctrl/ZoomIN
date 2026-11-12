using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>Caret marker geometry (E2): every shape at 1×, 4× and 16×, at the monitor edge, and when nothing is drawn.</summary>
public sealed class CaretMarkerGeometryTests
{
    private static readonly PhysicalRect s_area = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(CaretMarkerThickness.Thin, 1, 2)]
    [InlineData(CaretMarkerThickness.Thin, 16, 4)]
    [InlineData(CaretMarkerThickness.Medium, 1, 4)]
    [InlineData(CaretMarkerThickness.Medium, 4, 4)]
    [InlineData(CaretMarkerThickness.Medium, 6, 6)]
    [InlineData(CaretMarkerThickness.Medium, 16, 8)]
    [InlineData(CaretMarkerThickness.Thick, 1, 8)]
    [InlineData(CaretMarkerThickness.Thick, 16, 16)]
    public void LinePixels_GrowWithTheZoom_UpToTheCap(CaretMarkerThickness thickness, double zoom, double expected)
    {
        Assert.Equal(expected, CaretMarkerGeometry.LinePixels(thickness, zoom));
    }

    [Fact]
    public void LinePixels_UnknownThickness_CountsAsMedium()
    {
        Assert.Equal(4, CaretMarkerGeometry.LinePixels((CaretMarkerThickness)42, 2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void Bar_IsCentredOnTheCaret_AtLeastAsWideAsTheLine(double zoom)
    {
        var caret = Caret(zoom);

        var parts = CaretMarkerGeometry.Compute(caret, zoom, CaretMarkerShape.Bar, CaretMarkerThickness.Medium, s_area);

        var line = CaretMarkerGeometry.LinePixels(CaretMarkerThickness.Medium, zoom);
        Assert.Equal(CaretMarkerShape.Bar, parts.Shape);
        Assert.Equal(Math.Max(line, caret.Width), parts.First.Width, 6);
        Assert.Equal(caret.X + (caret.Width / 2), parts.First.X + (parts.First.Width / 2), 6);
        Assert.Equal(caret.Y, parts.First.Y);
        Assert.Equal(caret.Height, parts.First.Height);
    }

    [Fact]
    public void Bar_Thin_IsSlimmerThanMedium_AtHighZoom()
    {
        var caret = new PhysicalRect(400, 300, 8, 160); // 1 px caret at 8×

        var thin = CaretMarkerGeometry.Compute(caret, 8, CaretMarkerShape.Bar, CaretMarkerThickness.Thin, s_area);
        var medium = CaretMarkerGeometry.Compute(caret, 8, CaretMarkerShape.Bar, CaretMarkerThickness.Medium, s_area);

        Assert.Equal(4, thin.First.Width);
        Assert.Equal(8, medium.First.Width);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void Wedges_TouchTheCaretTopAndBottom(double zoom)
    {
        var caret = Caret(zoom);

        var parts = CaretMarkerGeometry.Compute(caret, zoom, CaretMarkerShape.Wedges, CaretMarkerThickness.Medium, s_area);

        Assert.Equal(caret.Y, parts.First.Bottom, 6);
        Assert.Equal(caret.Bottom, parts.Second.Y, 6);
        Assert.Equal(2 * parts.First.Height, parts.First.Width, 6);
        Assert.Equal(caret.X + (caret.Width / 2), parts.First.X + (parts.First.Width / 2), 6);
        Assert.Equal(parts.First.Width, parts.Second.Width);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void Frame_EnclosesTheEstimatedCharacter(double zoom)
    {
        var caret = Caret(zoom);

        var parts = CaretMarkerGeometry.Compute(caret, zoom, CaretMarkerShape.Frame, CaretMarkerThickness.Medium, s_area);

        var centerX = caret.X + (caret.Width / 2);
        Assert.True(parts.First.X < centerX);
        Assert.True(parts.First.Right > centerX + (caret.Height * 0.5));
        Assert.True(parts.First.Y < caret.Y);
        Assert.True(parts.First.Bottom > caret.Bottom);
        Assert.Equal(CaretMarkerGeometry.LinePixels(CaretMarkerThickness.Medium, zoom), parts.Line);
    }

    [Fact]
    public void Frame_WideCaret_UsesItsOwnWidth()
    {
        var caret = new PhysicalRect(100, 100, 12, 20);

        var parts = CaretMarkerGeometry.Compute(caret, 1, CaretMarkerShape.Frame, CaretMarkerThickness.Medium, s_area);

        // Line 4, pad 6 on each side of the 12 × 20 cell.
        Assert.Equal(new PhysicalRect(94, 94, 24, 32), parts.First);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void Circle_IsASquareAroundTheCaretCentre(double zoom)
    {
        var caret = Caret(zoom);

        var parts = CaretMarkerGeometry.Compute(caret, zoom, CaretMarkerShape.Circle, CaretMarkerThickness.Thick, s_area);

        Assert.Equal(parts.First.Width, parts.First.Height);
        Assert.Equal(caret.X + (caret.Width / 2), parts.First.X + (parts.First.Width / 2), 6);
        Assert.Equal(caret.Y + (caret.Height / 2), parts.First.Y + (parts.First.Height / 2), 6);
        Assert.True(parts.First.Width / 2 > caret.Height / 2);
    }

    [Fact]
    public void WedgeAbove_AtTheTopEdge_IsMovedInsideWhole()
    {
        var caret = new PhysicalRect(500, 0, 2, 40);

        var parts = CaretMarkerGeometry.Compute(caret, 2, CaretMarkerShape.Wedges, CaretMarkerThickness.Medium, s_area);

        Assert.Equal(0, parts.First.Y);
        Assert.Equal(16, parts.First.Height); // 2 × 4 px line + 0.2 × 40 px caret
    }

    [Fact]
    public void WedgeBelow_AtTheBottomEdge_IsMovedInside()
    {
        var caret = new PhysicalRect(500, 1060, 2, 20);

        var parts = CaretMarkerGeometry.Compute(caret, 1, CaretMarkerShape.Wedges, CaretMarkerThickness.Medium, s_area);

        Assert.Equal(s_area.Bottom, parts.Second.Bottom);
    }

    [Fact]
    public void Circle_AtTheLeftEdgeOfALens_StaysInsideTheLens()
    {
        var lens = new PhysicalRect(300, 200, 600, 400);
        var caret = new PhysicalRect(301, 300, 2, 40);

        var parts = CaretMarkerGeometry.Compute(caret, 4, CaretMarkerShape.Circle, CaretMarkerThickness.Medium, lens);

        Assert.Equal(lens.X, parts.First.X);
    }

    [Fact]
    public void PartLargerThanTheArea_AlignsTopLeft()
    {
        var tiny = new PhysicalRect(10, 10, 3, 5);

        var parts = CaretMarkerGeometry.Compute(new PhysicalRect(10, 10, 2, 4), 1, CaretMarkerShape.Bar, CaretMarkerThickness.Medium, tiny);

        // The 4 px bar is wider than the 3 px area.
        Assert.Equal(10, parts.First.X);
        Assert.Equal(10, parts.First.Y);
    }

    [Fact]
    public void InView_OnlyWithTheCaretMiddleInsideTheArea()
    {
        var lens = new PhysicalRect(300, 200, 600, 400);

        Assert.True(CaretMarkerGeometry.InView(new PhysicalRect(897, 300, 4, 40), lens)); // middle at x = 899
        Assert.False(CaretMarkerGeometry.InView(new PhysicalRect(899, 300, 4, 40), lens)); // middle at x = 901
        Assert.False(CaretMarkerGeometry.InView(new PhysicalRect(500, 590, 2, 40), lens)); // middle below
    }

    [Fact]
    public void UnknownShape_FallsBackToTheBar()
    {
        var parts = CaretMarkerGeometry.Compute(Caret(2), 2, (CaretMarkerShape)9, CaretMarkerThickness.Medium, s_area);

        Assert.Equal(CaretMarkerShape.Bar, parts.Shape);
    }

    [Fact]
    public void TryGetCaret_OnlyARealCaretWithTheMarkerOn()
    {
        var caret = new TrackingSnapshot(null, 0, Caret(1), 1);

        Assert.True(CaretMarkerGeometry.TryGetCaret(caret, TrackingOptions.Default, out var found));
        Assert.Equal(Caret(1), found);
        Assert.True(Draws(caret, TrackingOptions.Default));
        Assert.False(Draws(caret with { CaretEstimated = true }, TrackingOptions.Default));
        Assert.False(Draws(caret with { Caret = null }, TrackingOptions.Default));
        Assert.False(Draws(caret, TrackingOptions.Default with { CaretMarker = false }));
    }

    [Fact]
    public void Default_LooksLikeTheBarBeforeE2()
    {
        Assert.Equal(new CaretMarkerStyle(CaretMarkerShape.Bar, RgbColor.Magenta, CaretMarkerThickness.Medium), CaretMarkerStyle.Default);
        Assert.Equal(CaretMarkerStyle.Default, TrackingOptions.Default.CaretStyle);
        Assert.Equal(CaretMarkerStyle.Default, TrackingMapping.Options(new AppSettings()).CaretStyle);
    }

    [Fact]
    public void Mapping_UnreadableValues_FallBackToTheDefault()
    {
        var settings = new AppSettings
        {
            CaretMarkerShape = (CaretMarkerShape)17,
            CaretMarkerColor = "nope",
            CaretMarkerThickness = (CaretMarkerThickness)(-1),
        };

        Assert.Equal(CaretMarkerStyle.Default, TrackingMapping.Options(settings).CaretStyle);
    }

    [Fact]
    public void From_KeepsValidValues()
    {
        var style = CaretMarkerStyle.From(CaretMarkerShape.Frame, "#00E5FF", CaretMarkerThickness.Thin);

        Assert.Equal(new CaretMarkerStyle(CaretMarkerShape.Frame, RgbColor.Cyan, CaretMarkerThickness.Thin), style);
    }

    private static bool Draws(TrackingSnapshot tracking, TrackingOptions options) =>
        CaretMarkerGeometry.TryGetCaret(tracking, options, out _);

    // A 2 px caret, 20 px high on the desktop, as it appears magnified.
    private static PhysicalRect Caret(double zoom) => new(400, 300, 2 * zoom, 20 * zoom);
}
