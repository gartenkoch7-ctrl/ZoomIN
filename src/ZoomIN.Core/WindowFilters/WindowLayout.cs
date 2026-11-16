using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// Turns the visible windows (front to back) into <see cref="WindowRegions"/>: resolves each
/// window, lets popups show what their app window shows, and leaves out what cannot matter, so
/// the shader limit is rarely reached. Runs on the tracker thread after every burst of window
/// events (also while one is dragged), so it allocates only the arrays it hands out.
/// </summary>
public static class WindowLayout
{
    // Above this many windows the keep flags go to the heap; there are rarely more than 50.
    private const int StackLimit = 256;

    public static WindowRegions Build(IReadOnlyList<TrackedWindow> frontToBack, WindowFilterOptions options)
    {
        if (!options.Enabled)
        {
            return WindowRegions.Empty;
        }

        var palette = Resolve(frontToBack, options);
        return Build(frontToBack, palette, TitleBarResolver.Resolve(frontToBack, palette, options));
    }

    /// <summary>
    /// Builds the regions from palette indexes already resolved with <see cref="Resolve"/> and,
    /// optionally, title bar palettes from <see cref="TitleBarResolver.Resolve(IReadOnlyList{TrackedWindow}, byte[], WindowFilterOptions)"/>:
    /// a title bar that looks different gets its own rectangle right before its window's, so the
    /// shader (first rectangle wins) shows it instead of the window there.
    /// </summary>
    public static WindowRegions Build(IReadOnlyList<TrackedWindow> frontToBack, byte[] palette, byte[]? titleBars = null)
    {
        var count = frontToBack.Count;
        Span<bool> keep = count <= StackLimit ? stackalloc bool[count] : new bool[count];
        var kept = 0;
        var anyOwn = false;

        // From the bottom up: a window on the monitor filter only matters as a cover for a
        // filtered window further down; a window hidden by one window above never shows.
        for (var i = count - 1; i >= 0; i--)
        {
            var bounds = frontToBack[i].Bounds;
            if (RectMath.IsEmpty(bounds) || IsCoveredFromAbove(frontToBack, i))
            {
                continue;
            }

            var own = palette[i] != WindowPalette.Monitor;
            if (own || CoversFilteredBelow(frontToBack, palette, keep, i))
            {
                keep[i] = true;
                kept++;
                anyOwn |= own;
            }
        }

        if (!anyOwn)
        {
            return WindowRegions.Empty;
        }

        // The front-most windows win: what is dropped behind them falls back to the monitor filter.
        var needed = kept;
        for (var i = 0; i < count; i++)
        {
            needed += keep[i] && HasOwnTitleBar(titleBars, i) ? 1 : 0;
        }

        var taken = Math.Min(needed, WindowRegions.Max);
        var rects = new PhysicalRect[taken];
        var indexes = new byte[taken];
        var n = 0;
        for (var i = 0; i < count && n < taken; i++)
        {
            if (!keep[i])
            {
                continue;
            }

            // A title bar strip alone would only show the monitor filter, which is the fallback
            // anyway: with one slot left, the window itself is worth more.
            if (HasOwnTitleBar(titleBars, i) && n + 2 <= taken)
            {
                rects[n] = TitleBarResolver.Area(frontToBack[i]);
                indexes[n++] = titleBars![i];
            }

            rects[n] = frontToBack[i].Bounds;
            indexes[n++] = palette[i];
        }

        // Always full: a title bar is only skipped for the very last slot, which its window takes.
        return new WindowRegions(rects, indexes, needed - n);
    }

    /// <summary>Palette index per window; popups take their owner's, or the app window's below them.</summary>
    public static byte[] Resolve(IReadOnlyList<TrackedWindow> frontToBack, WindowFilterOptions options)
    {
        var palette = new byte[frontToBack.Count];

        // Owners lie below their popups, so going bottom-up resolves them first.
        for (var i = frontToBack.Count - 1; i >= 0; i--)
        {
            var window = frontToBack[i];
            palette[i] = window.Role == WindowRole.Popup
                ? Inherited(frontToBack, i, palette)
                : WindowFilterResolver.Resolve(window.Kind, window.Role, window.Brightness, options);
        }

        return palette;
    }

    private static byte Inherited(IReadOnlyList<TrackedWindow> frontToBack, int index, byte[] palette)
    {
        var popup = frontToBack[index];
        for (var i = index + 1; popup.Owner != 0 && i < frontToBack.Count; i++)
        {
            if (frontToBack[i].Handle == popup.Owner)
            {
                return palette[i];
            }
        }

        // Some menus have no owner (or it is hidden): the nearest window of the same app below stands in.
        for (var i = index + 1; i < frontToBack.Count; i++)
        {
            if (frontToBack[i].ProcessId == popup.ProcessId && frontToBack[i].Role != WindowRole.Popup)
            {
                return palette[i];
            }
        }

        return WindowPalette.Monitor;
    }

    private static bool HasOwnTitleBar(byte[]? titleBars, int index) =>
        titleBars is not null && titleBars[index] != TitleBarResolver.Same;

    private static bool IsCoveredFromAbove(IReadOnlyList<TrackedWindow> frontToBack, int index)
    {
        var bounds = frontToBack[index].Bounds;
        for (var i = 0; i < index; i++)
        {
            if (RectMath.Covers(frontToBack[i].Bounds, bounds))
            {
                return true;
            }
        }

        return false;
    }

    // Kept windows below with a filter of their own: those a monitor-filter window must hide.
    private static bool CoversFilteredBelow(IReadOnlyList<TrackedWindow> frontToBack, byte[] palette, Span<bool> keep, int index)
    {
        var bounds = frontToBack[index].Bounds;
        for (var i = index + 1; i < frontToBack.Count; i++)
        {
            if (keep[i] && palette[i] != WindowPalette.Monitor && RectMath.Intersects(bounds, frontToBack[i].Bounds))
            {
                return true;
            }
        }

        return false;
    }
}
