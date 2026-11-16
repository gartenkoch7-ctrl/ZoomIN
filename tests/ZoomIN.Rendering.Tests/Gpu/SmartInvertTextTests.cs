using static ZoomIN.Rendering.Tests.Gpu.SmartInvertRender;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// Smart inversion of text next to an edge between a dark bar and a white page (Excel: the dark row
/// numbers beside the white sheet). Text has the colour of the other side, so it must follow the
/// background around it, not its own colour.
/// </summary>
public sealed class SmartInvertTextTests
{
    // Dark column of row numbers left of x = 25, white sheet right of it; cell text starts 4 px later.
    private const int HeaderRight = 25;
    private const int TextLeft = 29;

    [Fact]
    public void LightDigits_OnTheDarkColumn_StayLight()
    {
        var pixels = Render(Sheet, SmartMonitor);

        for (var y = 40; y < 49; y++)
        {
            Assert.True(Luma(pixels.At(10, y)) > 200, $"digit at y={y} must stay light, got {Luma(pixels.At(10, y))}");
        }

        Assert.True(Luma(pixels.At(4, 20)) < 60, "the column itself stays dark");
    }

    [Fact]
    public void DarkCellText_NextToTheColumn_TurnsLight()
    {
        var pixels = Render(Sheet, SmartMonitor);

        for (var y = 40; y < 49; y++)
        {
            Assert.True(Luma(pixels.At(TextLeft, y)) > 200, $"first stroke at y={y} must turn light, got {Luma(pixels.At(TextLeft, y))}");
        }

        Assert.True(Luma(pixels.At(80, 100)) < 20, "the sheet is inverted");
    }

    [Fact]
    public void GapBetweenColumnAndText_IsInverted()
    {
        var pixels = Render(Sheet, SmartMonitor);

        for (var x = HeaderRight + 1; x < TextLeft; x++)
        {
            Assert.True(Luma(pixels.At(x, 44)) < 40, $"gap at x={x} must turn dark, got {Luma(pixels.At(x, 44))}");
        }
    }

    // Digits: 1 px strokes, 9 px high, in the dark column. Cell text: 1 px strokes every 4 px, the
    // first right after the 4 px margin, in rows 40–48 (a text line), on the white sheet.
    private static Bgra Sheet(int x, int y)
    {
        var line = y is >= 40 and < 49;
        if (x < HeaderRight)
        {
            return line && x is 10 or 14 ? White : Bar;
        }

        return line && x >= TextLeft && x < 70 && (x - TextLeft) % 4 == 0 ? Black : White;
    }
}
