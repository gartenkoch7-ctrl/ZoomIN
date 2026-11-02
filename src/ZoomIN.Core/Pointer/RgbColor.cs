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

    /// <summary>Suggested colours on the pointer page (M7 UI redesign).</summary>
    public static IReadOnlyList<RgbColor> Presets { get; } = [Yellow, Green, Cyan, Magenta, White, Black];

    public string ToHex() => string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}");

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
