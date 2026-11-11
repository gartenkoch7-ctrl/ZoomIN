using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Constant buffer b1 of Magnify.hlsl: window regions and the matrix palette (ADR-0020).
/// Filled in place by the render thread when regions or filters change; no allocation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WindowConstants
{
    public const int PaletteRows = 3 * WindowPalette.Count;

    /// <summary>A palette entry no region has; used as <see cref="SmartEntry"/> after a fall-back.</summary>
    public const uint NoEntry = uint.MaxValue;
    public static readonly int SizeInBytes = Unsafe.SizeOf<WindowConstants>();

    public uint RegionCount;
    public uint SmartEntry;

    /// <summary>Place + 1 of the own scheme whose replacements the monitor entry gets, 0 for none (M18).</summary>
    public uint MonitorScheme;

    /// <summary>1 if any own scheme replaces colours; 0 lets the shader skip the step.</summary>
    public uint ReplaceActive;
    public Regions Rects;
    public Regions Entries;
    public Rows Palette;
    public Schemes ReplaceCounts;
    public Replacements ReplaceFrom;
    public Replacements ReplaceShift;

    /// <summary>
    /// Writes <paramref name="regions"/> in the UV space of <paramref name="monitor"/> and the
    /// palette of <paramref name="effects"/>. Regions off this monitor are skipped.
    /// </summary>
    /// <param name="smartActive">False after a fall-back: no entry is smart, so smart regions show their matrix ("keep hue").</param>
    public static void Fill(ref WindowConstants target, WindowRegions regions, PhysicalRect monitor, DisplayEffects effects, bool smartActive = true)
    {
        var count = 0;
        var replacing = effects.ReplaceSlotOf(WindowPalette.Monitor) >= 0;
        var rects = regions.Rects;
        var palette = regions.Palette;
        for (var i = 0; i < rects.Length; i++)
        {
            var r = rects[i];
            if (!RectMath.Intersects(r, monitor))
            {
                continue;
            }

            replacing |= effects.ReplaceSlotOf(palette[i]) >= 0;

            target.Rects[count] = new Vector4(
                (float)((r.X - monitor.X) / monitor.Width),
                (float)((r.Y - monitor.Y) / monitor.Height),
                (float)((r.Right - monitor.X) / monitor.Width),
                (float)((r.Bottom - monitor.Y) / monitor.Height));
            target.Entries[count] = new Vector4(palette[i], 0, 0, 0);
            count++;
        }

        target.RegionCount = (uint)count;
        target.SmartEntry = smartActive ? WindowPalette.Smart : NoEntry;
        var matrices = effects.WindowMatrices;
        for (var i = 0; i < matrices.Length && (i * 3) + 2 < PaletteRows; i++)
        {
            var m = matrices[i];
            target.Palette[i * 3] = new Vector4(m.M00, m.M01, m.M02, m.OffsetR);
            target.Palette[(i * 3) + 1] = new Vector4(m.M10, m.M11, m.M12, m.OffsetG);
            target.Palette[(i * 3) + 2] = new Vector4(m.M20, m.M21, m.M22, m.OffsetB);
        }

        FillReplacements(ref target, effects, replacing);
    }

    // Own schemes (M18): the counts say how many entries of each place the shader reads. The step
    // runs only on a monitor where the monitor filter or a region on it uses replacements.
    private static void FillReplacements(ref WindowConstants target, DisplayEffects effects, bool replacing)
    {
        var table = effects.Replacements;
        target.ReplaceActive = replacing ? 1u : 0u;
        target.MonitorScheme = (uint)(effects.MonitorReplaceSlot + 1);
        for (var slot = 0; slot < CustomSchemes.Max; slot++)
        {
            target.ReplaceCounts[slot] = new UInt4((uint)table.CountOf(slot));
        }

        var items = table.Items;
        for (var i = 0; i < items.Length; i++)
        {
            var p = items[i];
            target.ReplaceFrom[i] = new Vector4(p.Y, p.Cb, p.Cr, p.Radius);
            target.ReplaceShift[i] = new Vector4(p.ShiftR, p.ShiftG, p.ShiftB, 0);
        }
    }

    [InlineArray(WindowRegions.Max)]
    public struct Regions
    {
        private Vector4 _element;
    }

    [InlineArray(PaletteRows)]
    public struct Rows
    {
        private Vector4 _element;
    }

    [InlineArray(CustomSchemes.Max)]
    public struct Schemes
    {
        private UInt4 _element;
    }

    [InlineArray(ReplacementTable.Size)]
    public struct Replacements
    {
        private Vector4 _element;
    }

    /// <summary>An HLSL <c>uint4</c>; only x is used, the rest pads to 16 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct UInt4(uint X, uint Y = 0, uint Z = 0, uint W = 0);
}
