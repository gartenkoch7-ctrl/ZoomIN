using System.Globalization;

namespace ZoomIN.Core.Pointer;

/// <summary>An opaque colour, stored as "#RRGGBB" in the settings.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor Yellow { get; } = new(255, 214, 0);
    public static RgbColor Green { get; } = new(0, 230, 118);
    public static RgbColor Cyan { get; } = new(0, 229, 255);
    public static RgbColor Magenta { get; } = new(255, 0, 255);
    public static RgbColor White { get; } = new(255, 255, 255);
    public static RgbColor Black { get; } = new(0, 0, 0);
    public static RgbColor Amber { get; } = new(255, 193, 7);

    // One list of colour and name, so the two can never get out of step.
    private static readonly (RgbColor Color, string Name)[] s_presets =
        [(Yellow, "Yellow"), (Green, "Green"), (Cyan, "Cyan"), (Magenta, "Magenta"), (White, "White"), (Black, "Black")];

    /// <summary>Suggested colours on the pointer page (M7 UI redesign).</summary>
    public static IReadOnlyList<RgbColor> Presets { get; } = Array.AsReadOnly(Array.ConvertAll(s_presets, p => p.Color));

    /// <summary>Name of a suggested colour (text key "Color." + name), or null for any other colour.</summary>
    public static string? PresetName(RgbColor color)
    {
        foreach (var (preset, name) in s_presets)
        {
            if (preset == color)
            {
                return name;
            }
        }

        return null;
    }

    public string ToHex() => string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}");

    /// <summary>The colour as 0–1 per channel, as shaders and colour matrices take it.</summary>
    public (float R, float G, float B) ToUnit() => (R / 255f, G / 255f, B / 255f);

    /// <summary>
    /// <paramref name="hex"/> as clean "#RRGGBB" (upper case), else <paramref name="fallback"/>, else
    /// white: what settings may keep of a colour read from a file.
    /// </summary>
    public static string NormalizedHex(string? hex, string fallback) =>
        ParseOrDefault(hex, ParseOrDefault(fallback, White)).ToHex();

    public static RgbColor ParseOrDefault(string? hex, RgbColor fallback)
    {
        if (hex is { Length: 7 } && hex[0] == '#'
            && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return new RgbColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        }

        return fallback;
    }
}
