using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Filters;

/// <summary>What an own scheme makes of a colour, as the shader does it: replacements, then the matrix (M18 preview).</summary>
public static class SchemePreview
{
    /// <summary>The colours of the preview on the scheme page: white, black, the primaries, yellow and grey.</summary>
    public static IReadOnlyList<RgbColor> Samples { get; } =
    [
        RgbColor.White, RgbColor.Black, new(255, 0, 0), new(0, 255, 0), new(0, 0, 255), new(255, 255, 0), new(128, 128, 128),
    ];

    /// <summary>Each of <paramref name="colors"/> as the scheme shows it; table and matrix are built once.</summary>
    public static RgbColor[] Apply(CustomScheme scheme, IReadOnlyList<RgbColor> colors)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(colors);
        var table = ReplacementTable.Build([scheme]);
        var matrix = SchemeMatrices.For(scheme);
        var slot = CustomSchemes.SlotOf(scheme.Id);
        var result = new RgbColor[colors.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var c = colors[i].ToUnit();
            var (r, g, b) = table.Apply(slot, c.R, c.G, c.B);
            var (fr, fg, fb) = matrix.Apply(r, g, b);
            result[i] = new RgbColor(Byte(fr), Byte(fg), Byte(fb));
        }

        return result;
    }

    private static byte Byte(float value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
