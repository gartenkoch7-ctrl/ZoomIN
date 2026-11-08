namespace ZoomIN.Core.Pointer;

/// <summary>
/// Colour rule of the adaptive pointer (M15, ADR-0026), shared by the preview and Cursor.hlsl:
/// the background decides which colour of the pair is the body; light pointer pixels are body,
/// dark ones outline, and the outline takes the other colour.
/// </summary>
public static class AdaptiveColor
{
    // Rec. 709 luma weights, as in the colour filters and the shader.
    private const double Lr = 0.2126;
    private const double Lg = 0.7152;
    private const double Lb = 0.0722;

    /// <summary>Brightness 0–1 of a colour given as 0–255 channels.</summary>
    public static double Luma(double r, double g, double b) => ((Lr * r) + (Lg * g) + (Lb * b)) / 255;

    /// <summary>True: the background counts as light, the body takes <see cref="PointerStyle.AdaptiveOnLight"/>.</summary>
    public static bool OnLight(double backgroundLuma, PointerStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return backgroundLuma >= style.AdaptiveThreshold;
    }

    /// <summary>Colour (0–255, not premultiplied) for one pointer pixel.</summary>
    /// <param name="ownLuma">Brightness 0–1 of the pointer pixel: 1 = body, 0 = outline.</param>
    /// <param name="backgroundLuma">Brightness 0–1 of what is shown under it.</param>
    /// <param name="style">Colour pair and threshold.</param>
    public static (double R, double G, double B) Pick(double ownLuma, double backgroundLuma, PointerStyle style)
    {
        var light = OnLight(backgroundLuma, style);
        var body = light ? style.AdaptiveOnLight : style.AdaptiveOnDark;
        var outline = light ? style.AdaptiveOnDark : style.AdaptiveOnLight;
        var t = Math.Clamp(ownLuma, 0, 1);
        return (Mix(outline.R, body.R, t), Mix(outline.G, body.G, t), Mix(outline.B, body.B, t));
    }

    private static double Mix(byte from, byte to, double t) => from + ((to - from) * t);
}
