using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// Immutable snapshot for the render threads (AGENTS.md 2.3): window rectangles front to back,
/// each with a <see cref="WindowPalette"/> index. The shader takes the first rectangle that holds
/// a pixel, so windows above hide the ones below. Pixels in no rectangle show the monitor filter.
/// </summary>
public sealed class WindowRegions
{
    /// <summary>The most rectangles the shader takes; further windows fall back to the monitor filter.</summary>
    public const int Max = 64;

    private readonly PhysicalRect[] _rects;
    private readonly byte[] _palette;

    /// <param name="rects">Window rectangles, front to back.</param>
    /// <param name="palette">One palette index per rectangle.</param>
    /// <param name="dropped">Windows left out before because of <see cref="Max"/> (for telemetry).</param>
    public WindowRegions(ReadOnlySpan<PhysicalRect> rects, ReadOnlySpan<byte> palette, int dropped = 0)
    {
        if (rects.Length != palette.Length)
        {
            throw new ArgumentException("One palette index per rectangle.", nameof(palette));
        }

        Count = Math.Min(rects.Length, Max);
        Dropped = dropped + (rects.Length - Count);
        _rects = rects[..Count].ToArray();
        _palette = palette[..Count].ToArray();
    }

    /// <summary>Nothing to filter per window: the monitor decides alone (and may pause).</summary>
    public static WindowRegions Empty { get; } = new([], []);

    public int Count { get; }

    /// <summary>Windows that needed a region but did not get one (over <see cref="Max"/>); they show the monitor filter.</summary>
    public int Dropped { get; }

    public ReadOnlySpan<PhysicalRect> Rects => _rects;

    public ReadOnlySpan<byte> Palette => _palette;

    /// <summary>
    /// True if some window on <paramref name="monitor"/> looks different from the rest of the
    /// monitor with <paramref name="effects"/>; if not, the overlay may pause. Per frame, no allocation.
    /// </summary>
    public bool ChangesMonitor(PhysicalRect monitor, DisplayEffects effects)
    {
        var matrices = effects.WindowMatrices;
        var monitorMatrix = matrices[WindowPalette.Monitor];
        for (var i = 0; i < Count; i++)
        {
            if (_palette[i] != WindowPalette.Monitor
                && RectMath.Intersects(_rects[i], monitor)
                && matrices[_palette[i]] != monitorMatrix)
            {
                return true;
            }
        }

        return false;
    }

    public bool ContentEquals(WindowRegions other) =>
        ReferenceEquals(this, other)
        || (Count == other.Count && Rects.SequenceEqual(other.Rects) && Palette.SequenceEqual(other.Palette));
}
