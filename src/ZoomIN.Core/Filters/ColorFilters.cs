namespace ZoomIN.Core.Filters;

/// <summary>Builds the colour matrix for a filter plus contrast and brightness.</summary>
public static class ColorFilters
{
    // Rec. 709 luma weights: how bright each channel looks.
    private const float Lr = 0.2126f;
    private const float Lg = 0.7152f;
    private const float Lb = 0.0722f;

    private static readonly ColorMatrix s_invert = new(-1, 0, 0, 0, -1, 0, 0, 0, -1, 1, 1, 1);
    private static readonly ColorMatrix s_luma = new(Lr, Lg, Lb, Lr, Lg, Lb, Lr, Lg, Lb, 0, 0, 0);
    private static readonly ColorMatrix s_lumaInverted = new(-Lr, -Lg, -Lb, -Lr, -Lg, -Lb, -Lr, -Lg, -Lb, 1, 1, 1);

    // Hue rotation by 180° that keeps luminance (feColorMatrix "hueRotate" with cos = −1, sin = 0).
    private static readonly ColorMatrix s_hueRotate180 = new(
        -0.574f, 1.430f, 0.144f,
        0.426f, 0.430f, 0.144f,
        0.426f, 1.430f, -0.856f,
        0, 0, 0);

    /// <summary>
    /// Order in lists and for "next filter": the smart inversion right after the plain one. The enum
    /// order stays as it is, since palette indexes and saved settings depend on it.
    /// </summary>
    public static IReadOnlyList<ColorFilter> DisplayOrder { get; } =
    [
        ColorFilter.Inverted, ColorFilter.InvertedSmart, ColorFilter.InvertedKeepHue, ColorFilter.Grayscale,
        ColorFilter.GrayscaleInverted, ColorFilter.YellowOnBlack, ColorFilter.BlackOnYellow, ColorFilter.WhiteOnBlue,
    ];

    /// <summary>Position of <paramref name="filter"/> in <see cref="DisplayOrder"/>.</summary>
    public static int DisplayIndexOf(ColorFilter filter)
    {
        for (var i = 0; i < DisplayOrder.Count; i++)
        {
            if (DisplayOrder[i] == filter)
            {
                return i;
            }
        }

        return 0;
    }

    public static ColorMatrix For(ColorFilter filter) => filter switch
    {
        ColorFilter.Inverted => s_invert,
        ColorFilter.InvertedKeepHue => s_hueRotate180.After(s_invert),

        // Where the shader inverts at all (ADR-0024); also what is left when it cannot decide.
        ColorFilter.InvertedSmart => s_hueRotate180.After(s_invert),
        ColorFilter.Grayscale => s_luma,
        ColorFilter.GrayscaleInverted => s_lumaInverted,

        // Inverted luma in red and green only: light text turns yellow, white background black.
        ColorFilter.YellowOnBlack => new(-Lr, -Lg, -Lb, -Lr, -Lg, -Lb, 0, 0, 0, 1, 1, 0),

        // Luma in red and green only: black text stays black, white background turns yellow.
        ColorFilter.BlackOnYellow => new(Lr, Lg, Lb, Lr, Lg, Lb, 0, 0, 0, 0, 0, 0),

        // Inverted luma, background (luma 1) becomes dark blue (0, 0, 0.5).
        ColorFilter.WhiteOnBlue => new(-Lr, -Lg, -Lb, -Lr, -Lg, -Lb, -Lr / 2, -Lg / 2, -Lb / 2, 1, 1, 1),
        _ => ColorMatrix.Identity,
    };

    /// <summary>
    /// Filter (if enabled), then contrast around mid-grey and brightness offset.
    /// Contrast 1 and brightness 0 leave colours unchanged.
    /// </summary>
    public static ColorMatrix Build(FilterSettings settings) =>
        Adjust(settings).After(settings.Enabled ? For(settings.Filter) : ColorMatrix.Identity);

    /// <summary>Contrast and brightness only; what windows with "None" get (they still follow both).</summary>
    public static ColorMatrix Adjust(FilterSettings settings)
    {
        var c = (float)settings.Contrast;
        var offset = (0.5f * (1 - c)) + (float)settings.Brightness;
        return new ColorMatrix(c, 0, 0, 0, c, 0, 0, 0, c, offset, offset, offset);
    }
}
