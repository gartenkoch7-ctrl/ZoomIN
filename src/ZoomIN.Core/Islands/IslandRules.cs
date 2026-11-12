using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Islands;

/// <summary>
/// The rules every island follows (M19 B1): at most <see cref="MaxCount"/>, a minimum size, the
/// level within <see cref="ZoomLimits"/>, source and target inside the island's monitor, whole
/// pixels. Pure functions; the editor, the settings and a display change all go through
/// <see cref="Fit"/>, so a broken settings file or a smaller monitor never yields an island that
/// lies outside its monitor.
/// </summary>
public static class IslandRules
{
    /// <summary>Most islands at once (grilling 2026-11-11).</summary>
    public const int MaxCount = 4;

    /// <summary>Smallest source side in pixels; a smaller drag is a click, not a new island.</summary>
    public const double MinSize = 16;

    /// <summary>
    /// Puts <paramref name="island"/> inside <paramref name="monitor"/>: the source is clipped to it
    /// (and grown to <see cref="MinSize"/>), the level clamped so the picture fits, and the picture
    /// pushed back onto the monitor. Non-finite values fall back to safe ones.
    /// </summary>
    public static Island Fit(Island island, PhysicalRect monitor)
    {
        ArgumentNullException.ThrowIfNull(island);
        var source = FitSource(island.Source, monitor);
        var level = double.IsFinite(island.Level) ? ZoomLimits.Clamp(island.Level) : ZoomLimits.DefaultLevel;

        // The source lies inside the monitor, so the bound is at least 1×.
        level = Math.Min(level, Math.Min(monitor.Width / source.Width, monitor.Height / source.Height));
        var origin = island.TargetOrigin;
        var x = double.IsFinite(origin.X) ? origin.X : source.X;
        var y = double.IsFinite(origin.Y) ? origin.Y : source.Y;
        // Max: a level capped to the monitor may give a picture one rounding step wider than it.
        x = Math.Round(Math.Clamp(x, monitor.X, Math.Max(monitor.X, monitor.Right - (source.Width * level))));
        y = Math.Round(Math.Clamp(y, monitor.Y, Math.Max(monitor.Y, monitor.Bottom - (source.Height * level))));
        return island with { Source = source, TargetOrigin = new PhysicalPoint(x, y), Level = level };
    }

    /// <summary>
    /// A new island for the dragged <paramref name="source"/>: the picture grows around the centre
    /// of the source, so the island covers what it magnifies, and is pushed onto the monitor at its
    /// edges (a clock in the corner stays in the corner).
    /// </summary>
    public static Island Create(int id, string monitorId, PhysicalRect source, PhysicalRect monitor, double level)
    {
        var fitted = FitSource(source, monitor);
        var centre = new PhysicalPoint(fitted.X + (fitted.Width / 2), fitted.Y + (fitted.Height / 2));
        var origin = new PhysicalPoint(centre.X - (fitted.Width * level / 2), centre.Y - (fitted.Height * level / 2));
        return Fit(new Island(id, monitorId, fitted, origin, level), monitor);
    }

    /// <summary>A new level; the picture keeps its centre (then is pushed onto the monitor).</summary>
    public static Island WithLevel(Island island, double level, PhysicalRect monitor)
    {
        ArgumentNullException.ThrowIfNull(island);
        var target = island.Target;
        var centre = new PhysicalPoint(target.X + (target.Width / 2), target.Y + (target.Height / 2));
        var clamped = double.IsFinite(level) ? ZoomLimits.Clamp(level) : island.Level;
        var origin = new PhysicalPoint(
            centre.X - (island.Source.Width * clamped / 2), centre.Y - (island.Source.Height * clamped / 2));
        return Fit(island with { Level = clamped, TargetOrigin = origin }, monitor);
    }

    /// <summary>The source clipped to the monitor, at least <see cref="MinSize"/> (or the monitor) wide and high, whole pixels.</summary>
    public static PhysicalRect FitSource(PhysicalRect source, PhysicalRect monitor)
    {
        var width = Side(source.Width, monitor.Width);
        var height = Side(source.Height, monitor.Height);
        var x = double.IsFinite(source.X) ? Math.Round(source.X) : monitor.X;
        var y = double.IsFinite(source.Y) ? Math.Round(source.Y) : monitor.Y;

        // Clip first (a source half off the monitor keeps its visible part), then fit the minimum.
        var right = Math.Min(x + width, monitor.Right);
        var bottom = Math.Min(y + height, monitor.Bottom);
        x = Math.Max(x, monitor.X);
        y = Math.Max(y, monitor.Y);
        width = Math.Max(right - x, Math.Min(MinSize, monitor.Width));
        height = Math.Max(bottom - y, Math.Min(MinSize, monitor.Height));
        x = Math.Clamp(x, monitor.X, monitor.Right - width);
        y = Math.Clamp(y, monitor.Y, monitor.Bottom - height);
        return new PhysicalRect(x, y, width, height);
    }

    /// <summary>
    /// True if source and picture lie on <paramref name="monitor"/> (half a pixel of tolerance:
    /// <see cref="Fit"/> rounds to whole pixels, a monitor may have a fractional origin). Islands
    /// that do not are not drawn and cannot be grabbed. Per frame: no allocation.
    /// </summary>
    public static bool Fits(Island island, PhysicalRect monitor)
    {
        ArgumentNullException.ThrowIfNull(island);
        var source = island.Source;
        var picture = island.Target;
        return monitor.Width > 0 && source.Width > 0 && source.Height > 0
            && Within(source, monitor) && Within(picture, monitor);
    }

    /// <summary>
    /// Level of a new island (plan decision): what the monitor shows when zoomed in – the user's level,
    /// while zoom is off the last fullscreen level, else <see cref="ZoomLimits.DefaultLevel"/>.
    /// </summary>
    public static double StartLevel(ZoomModel zoom, string monitorId)
    {
        ArgumentNullException.ThrowIfNull(zoom);
        var level = zoom.TargetFor(monitorId);
        level = level > ZoomLimits.Min ? level : zoom.LastLevelFor(Modes.ZoomMode.Fullscreen, monitorId);
        return level > ZoomLimits.Min ? level : ZoomLimits.DefaultLevel;
    }

    /// <summary>True if a drag from one corner to the other is large enough for an island.</summary>
    public static bool IsLargeEnough(PhysicalRect dragged) => dragged.Width >= MinSize && dragged.Height >= MinSize;

    private static bool Within(PhysicalRect rect, PhysicalRect monitor) =>
        rect.X >= monitor.X - 0.5 && rect.Right <= monitor.Right + 0.5 && rect.Y >= monitor.Y - 0.5 && rect.Bottom <= monitor.Bottom + 0.5;

    private static double Side(double value, double monitor) =>
        double.IsFinite(value) && value > 0 ? Math.Min(Math.Round(value), monitor) : Math.Min(MinSize, monitor);
}
