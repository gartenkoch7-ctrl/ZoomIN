using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// One look at a window (ADR-0020): of the pixels that really show the window – not covered by
/// windows above it, not outside every monitor – what share is light (luma ≥ 80 %)?
/// </summary>
public static class BrightnessMeter
{
    /// <summary>Luma from which a pixel counts as light: 80 % of 255.</summary>
    public const int LightLuma = 204;

    /// <summary>Less visible share than this (in %) and the look counts as failed.</summary>
    public const int MinVisiblePercent = 15;

    /// <param name="pixels">Shrunk screenshot of <paramref name="captured"/>, 32-bit 0xAARRGGBB, rows top-down.</param>
    /// <param name="width">Pixels per row.</param>
    /// <param name="height">Rows.</param>
    /// <param name="captured">Desktop area the pixels show.</param>
    /// <param name="above">Windows above the measured one that cover parts of it.</param>
    /// <param name="monitors">Monitor bounds; what lies outside all of them is not seen.</param>
    /// <param name="thresholdPercent">Light share from which the window counts as light.</param>
    /// <returns>True for light, false for dark, null if too little of the window was visible.</returns>
    public static bool? IsLight(
        ReadOnlySpan<uint> pixels, int width, int height, PhysicalRect captured,
        ReadOnlySpan<PhysicalRect> above, ReadOnlySpan<PhysicalRect> monitors, int thresholdPercent)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height || RectMath.IsEmpty(captured))
        {
            return null;
        }

        var visible = 0;
        var light = 0;
        for (var y = 0; y < height; y++)
        {
            var py = captured.Y + ((y + 0.5) * captured.Height / height);
            for (var x = 0; x < width; x++)
            {
                var point = new PhysicalPoint(captured.X + ((x + 0.5) * captured.Width / width), py);
                if (!IsSeen(point, above, monitors))
                {
                    continue;
                }

                visible++;
                if (Luma(pixels[(y * width) + x]) >= LightLuma)
                {
                    light++;
                }
            }
        }

        if (visible * 100 < MinVisiblePercent * width * height)
        {
            return null;
        }

        return light * 100 >= thresholdPercent * visible;
    }

    /// <summary>
    /// Like <see cref="IsLight"/>, but the rows above <paramref name="titleBarBottom"/> (the title
    /// bar) are counted on their own (ADR-0023): only the page below decides light or dark, so a
    /// dark title bar (dark Windows theme) does not make a light page dark. A row cut by the edge
    /// belongs to neither part.
    /// </summary>
    /// <param name="titleBarBottom">Desktop y where the title bar ends; at or above the top of <paramref name="captured"/> = none.</param>
    /// <returns>The page and the title bar (null if not visible enough or none).</returns>
    public static (bool? Page, bool? TitleBar) IsLightApart(
        ReadOnlySpan<uint> pixels, int width, int height, PhysicalRect captured, double titleBarBottom,
        ReadOnlySpan<PhysicalRect> above, ReadOnlySpan<PhysicalRect> monitors, int thresholdPercent)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height || RectMath.IsEmpty(captured))
        {
            return (null, null);
        }

        var rowHeight = captured.Height / height;
        var split = Math.Min((titleBarBottom - captured.Y) / rowHeight, height);
        if (!(split >= 1))
        {
            // No title bar (also -∞ or NaN), or less than one row of it: the whole frame counts.
            return (IsLight(pixels, width, height, captured, above, monitors, thresholdPercent), null);
        }

        var titleRows = (int)Math.Floor(split);
        var pageStart = (int)Math.Ceiling(split);

        var titleArea = new PhysicalRect(captured.X, captured.Y, captured.Width, titleRows * rowHeight);
        var titleBar = IsLight(pixels[..(titleRows * width)], width, titleRows, titleArea, above, monitors, thresholdPercent);
        if (pageStart >= height)
        {
            return (null, titleBar);
        }

        var pageArea = new PhysicalRect(captured.X, captured.Y + (pageStart * rowHeight), captured.Width, (height - pageStart) * rowHeight);
        var page = IsLight(pixels[(pageStart * width)..(height * width)], width, height - pageStart, pageArea, above, monitors, thresholdPercent);
        return (page, titleBar);
    }

    /// <summary>Rec. 709 luma of a 0xAARRGGBB pixel, 0–255.</summary>
    public static int Luma(uint pixel)
    {
        var r = (int)((pixel >> 16) & 0xFF);
        var g = (int)((pixel >> 8) & 0xFF);
        var b = (int)(pixel & 0xFF);
        return ((2126 * r) + (7152 * g) + (722 * b)) / 10000;
    }

    private static bool IsSeen(PhysicalPoint point, ReadOnlySpan<PhysicalRect> above, ReadOnlySpan<PhysicalRect> monitors)
    {
        foreach (var cover in above)
        {
            if (cover.Contains(point))
            {
                return false;
            }
        }

        foreach (var monitor in monitors)
        {
            if (monitor.Contains(point))
            {
                return true;
            }
        }

        return false;
    }
}
