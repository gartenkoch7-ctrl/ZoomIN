using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Filters;

/// <summary>
/// The colour replacements of all own schemes, prepared for the shader (M18, C3): per place up to
/// three entries, each the YCbCr of the colour to catch, a radius from the tolerance and the RGB
/// shift towards the new colour. Built when settings change; render threads only copy it.
/// </summary>
/// <remarks>
/// A pixel is shifted by <c>(to − from)·w</c>, w = 1 − smoothstep(r/2, r, distance) in YCbCr: the
/// colour itself becomes exactly the new one, anti-aliased edges and shades move along with it.
/// Several replacements add up independently of their order. Same formula as Magnify.hlsl.
/// </remarks>
public sealed class ReplacementTable
{
    public const int PerScheme = CustomScheme.MaxReplacements;
    public const int Size = CustomSchemes.Max * PerScheme;

    // Radius in YCbCr at tolerance 0 and how much 100 % adds. At 0 only the colour itself and
    // what differs by less than about one step of 8-bit grey is caught.
    private const float MinRadius = 0.01f;
    private const float RadiusPerTolerance = 0.5f;

    private readonly PackedReplacement[] _items = new PackedReplacement[Size];
    private readonly int[] _counts = new int[CustomSchemes.Max];

    private ReplacementTable()
    {
    }

    public static ReplacementTable Empty { get; } = new();

    /// <summary>Some scheme replaces colours; without, the shader skips the step.</summary>
    public bool Any { get; private set; }

    /// <summary>Entries in place order, <see cref="PerScheme"/> per place; unused ones are zero.</summary>
    public ReadOnlySpan<PackedReplacement> Items => _items;

    /// <summary>Replacements of the scheme in place <paramref name="slot"/> (0–3); 0 for other values.</summary>
    public int CountOf(int slot) => (uint)slot < (uint)_counts.Length ? _counts[slot] : 0;

    public static ReplacementTable Build(IReadOnlyList<CustomScheme> schemes)
    {
        ArgumentNullException.ThrowIfNull(schemes);
        var any = false;
        var table = new ReplacementTable();
        foreach (var scheme in schemes)
        {
            var slot = CustomSchemes.SlotOf(scheme.Id);
            if (slot < 0)
            {
                continue;
            }

            var count = Math.Min(scheme.Replacements.Count, PerScheme);
            for (var i = 0; i < count; i++)
            {
                table._items[(slot * PerScheme) + i] = Pack(scheme.Replacements[i]);
            }

            table._counts[slot] = count;
            any |= count > 0;
        }

        table.Any = any;
        return any ? table : Empty;
    }

    public static float Radius(int tolerance) =>
        MinRadius + (RadiusPerTolerance * Math.Clamp(tolerance, 0, ColorReplacement.MaxTolerance) / ColorReplacement.MaxTolerance);

    /// <summary>Rec. 709 YCbCr of an RGB colour (0–1): luma and two colour differences of ±0.5.</summary>
    public static (float Y, float Cb, float Cr) Ycc(float r, float g, float b)
    {
        var y = (ColorFilters.Lr * r) + (ColorFilters.Lg * g) + (ColorFilters.Lb * b);
        return (y, (b - y) / 1.8556f, (r - y) / 1.5748f);
    }

    public static PackedReplacement Pack(ColorReplacement replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var from = RgbColor.ParseOrDefault(replacement.From, RgbColor.White).ToUnit();
        var to = RgbColor.ParseOrDefault(replacement.To, RgbColor.White).ToUnit();
        var (y, cb, cr) = Ycc(from.R, from.G, from.B);
        return new PackedReplacement(y, cb, cr, Radius(replacement.Tolerance), to.R - from.R, to.G - from.G, to.B - from.B);
    }

    /// <summary>What the shader does to a colour (0–1) for the scheme in <paramref name="slot"/>; the CPU reference for tests.</summary>
    public (float R, float G, float B) Apply(int slot, float r, float g, float b)
    {
        var (y, cb, cr) = Ycc(r, g, b);
        float sr = 0, sg = 0, sb = 0;
        for (var i = 0; i < CountOf(slot); i++)
        {
            var p = _items[(slot * PerScheme) + i];
            var distance = MathF.Sqrt(((y - p.Y) * (y - p.Y)) + ((cb - p.Cb) * (cb - p.Cb)) + ((cr - p.Cr) * (cr - p.Cr)));
            var w = 1 - SmoothStep(p.Radius / 2, p.Radius, distance);
            (sr, sg, sb) = (sr + (p.ShiftR * w), sg + (p.ShiftG * w), sb + (p.ShiftB * w));
        }

        return (Math.Clamp(r + sr, 0, 1), Math.Clamp(g + sg, 0, 1), Math.Clamp(b + sb, 0, 1));
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - (2 * t));
    }
}
