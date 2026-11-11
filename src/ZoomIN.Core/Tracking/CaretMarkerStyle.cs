using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Tracking;

/// <summary>Look of the caret marker (E2). Part of the profile.</summary>
/// <param name="Shape">Bar, wedges, frame or circle.</param>
/// <param name="Color">Opaque colour; the marker is drawn with <see cref="Opacity"/>.</param>
/// <param name="Thickness">Base line strength, multiplied by the zoom.</param>
public sealed record CaretMarkerStyle(CaretMarkerShape Shape, RgbColor Color, CaretMarkerThickness Thickness)
{
    /// <summary>The marker stays slightly translucent so the letters under it remain readable (M6).</summary>
    public const double Opacity = 0.85;

    /// <summary>Factory setting: looks like the magenta bar before E2.</summary>
    public static CaretMarkerStyle Default { get; } = new(CaretMarkerShape.Bar, RgbColor.Magenta, CaretMarkerThickness.Medium);

    /// <summary>
    /// A style from stored or entered values; unknown or unreadable ones are taken from
    /// <paramref name="fallback"/> (<see cref="Default"/> if none).
    /// </summary>
    public static CaretMarkerStyle From(
        CaretMarkerShape shape, string? colorHex, CaretMarkerThickness thickness, CaretMarkerStyle? fallback = null)
    {
        var f = fallback ?? Default;
        return new(
            Enum.IsDefined(shape) ? shape : f.Shape,
            RgbColor.ParseOrDefault(colorHex, f.Color),
            Enum.IsDefined(thickness) ? thickness : f.Thickness);
    }
}
