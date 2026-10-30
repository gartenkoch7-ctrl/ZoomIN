namespace ZoomIN.Core.Filters;

/// <summary>
/// Magnified pointer (M5 decision): the real Windows shape, growing with the zoom up to
/// <see cref="MaxScale"/>, with a thin contrast rim.
/// </summary>
public sealed record CursorStyle(double MaxScale, bool Rim, bool AutoLocate)
{
    public static CursorStyle Default { get; } = new(4.0, true, false);

    /// <summary>Pointer scale for a zoom level: grows with the zoom, never above the maximum.</summary>
    public double ScaleFor(double zoom) => Math.Clamp(zoom, 1.0, Math.Max(1.0, MaxScale));
}
