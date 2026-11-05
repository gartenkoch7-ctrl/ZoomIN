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
    public const int PaletteRows = 3 * 9;
    public static readonly int SizeInBytes = Unsafe.SizeOf<WindowConstants>();

    public uint RegionCount;
    public Vector3 Padding;
    public Regions Rects;
    public Regions Entries;
    public Rows Palette;

    /// <summary>
    /// Writes <paramref name="regions"/> in the UV space of <paramref name="monitor"/> and the
    /// palette of <paramref name="effects"/>. Regions off this monitor are skipped.
    /// </summary>
    public static void Fill(ref WindowConstants target, WindowRegions regions, PhysicalRect monitor, DisplayEffects effects)
    {
        var count = 0;
        var rects = regions.Rects;
        var palette = regions.Palette;
        for (var i = 0; i < rects.Length; i++)
        {
            var r = rects[i];
            if (!RectMath.Intersects(r, monitor))
            {
                continue;
            }

            target.Rects[count] = new Vector4(
                (float)((r.X - monitor.X) / monitor.Width),
                (float)((r.Y - monitor.Y) / monitor.Height),
                (float)((r.Right - monitor.X) / monitor.Width),
                (float)((r.Bottom - monitor.Y) / monitor.Height));
            target.Entries[count] = new Vector4(palette[i], 0, 0, 0);
            count++;
        }

        target.RegionCount = (uint)count;
        var matrices = effects.WindowMatrices;
        for (var i = 0; i < matrices.Length && (i * 3) + 2 < PaletteRows; i++)
        {
            var m = matrices[i];
            target.Palette[i * 3] = new Vector4(m.M00, m.M01, m.M02, m.OffsetR);
            target.Palette[(i * 3) + 1] = new Vector4(m.M10, m.M11, m.M12, m.OffsetG);
            target.Palette[(i * 3) + 2] = new Vector4(m.M20, m.M21, m.M22, m.OffsetB);
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
}
