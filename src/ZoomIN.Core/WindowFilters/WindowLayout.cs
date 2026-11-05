using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// Turns the visible windows (front to back) into <see cref="WindowRegions"/>: resolves each
/// window, lets popups show what their app window shows, and leaves out what cannot matter, so
/// the shader limit is rarely reached. Runs on the tracker thread, not per frame.
/// </summary>
public static class WindowLayout
{
    public static WindowRegions Build(IReadOnlyList<TrackedWindow> frontToBack, WindowFilterOptions options) =>
        options.Enabled ? Build(frontToBack, Resolve(frontToBack, options)) : WindowRegions.Empty;

    /// <summary>Builds the regions from palette indexes already resolved with <see cref="Resolve"/>.</summary>
    public static WindowRegions Build(IReadOnlyList<TrackedWindow> frontToBack, byte[] palette)
    {
        if (frontToBack.Count == 0)
        {
            return WindowRegions.Empty;
        }

        var keep = new bool[frontToBack.Count];
        var anyOwn = false;

        // From the bottom up: a window on the monitor filter only matters as a cover for a
        // filtered window further down; a window hidden by one window above never shows.
        var filteredBelow = new List<PhysicalRect>();
        for (var i = frontToBack.Count - 1; i >= 0; i--)
        {
            var bounds = frontToBack[i].Bounds;
            if (RectMath.IsEmpty(bounds) || IsCoveredFromAbove(frontToBack, i))
            {
                continue;
            }

            if (palette[i] != WindowPalette.Monitor)
            {
                keep[i] = true;
                anyOwn = true;
                filteredBelow.Add(bounds);
            }
            else if (OverlapsAny(bounds, filteredBelow))
            {
                keep[i] = true;
            }
        }

        if (!anyOwn)
        {
            return WindowRegions.Empty;
        }

        var rects = new List<PhysicalRect>();
        var indexes = new List<byte>();
        for (var i = 0; i < frontToBack.Count && rects.Count < WindowRegions.Max; i++)
        {
            if (keep[i])
            {
                rects.Add(frontToBack[i].Bounds);
                indexes.Add(palette[i]);
            }
        }

        return new WindowRegions(rects.ToArray(), indexes.ToArray());
    }

    /// <summary>Palette index per window; popups take their owner's, or the app window's below them.</summary>
    public static byte[] Resolve(IReadOnlyList<TrackedWindow> frontToBack, WindowFilterOptions options)
    {
        var palette = new byte[frontToBack.Count];
        var byHandle = new Dictionary<nint, byte>();

        // Owners lie below their popups, so going bottom-up resolves them first.
        for (var i = frontToBack.Count - 1; i >= 0; i--)
        {
            var window = frontToBack[i];
            palette[i] = window.Role == WindowRole.Popup
                ? Inherited(frontToBack, i, byHandle, palette)
                : WindowFilterResolver.Resolve(window.Kind, window.Role, window.Brightness, options);
            byHandle[window.Handle] = palette[i];
        }

        return palette;
    }

    private static byte Inherited(IReadOnlyList<TrackedWindow> frontToBack, int index, Dictionary<nint, byte> resolved, byte[] palette)
    {
        var popup = frontToBack[index];
        if (popup.Owner != 0 && resolved.TryGetValue(popup.Owner, out var owner))
        {
            return owner;
        }

        // Some menus have no owner: the nearest window of the same app below stands in.
        for (var i = index + 1; i < frontToBack.Count; i++)
        {
            if (frontToBack[i].ProcessId == popup.ProcessId && frontToBack[i].Role != WindowRole.Popup)
            {
                return palette[i];
            }
        }

        return WindowPalette.Monitor;
    }

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

    private static bool OverlapsAny(PhysicalRect bounds, List<PhysicalRect> others)
    {
        foreach (var other in others)
        {
            if (RectMath.Intersects(bounds, other))
            {
                return true;
            }
        }

        return false;
    }
}
