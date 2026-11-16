namespace ZoomIN.Core.Filters;

/// <summary>Builds the colour matrix for a filter plus contrast and brightness.</summary>
public static class ColorFilters
{
    // Rec. 709 luma weights: how bright each channel looks.
    internal const float Lr = 0.2126f;
    internal const float Lg = 0.7152f;
    internal const float Lb = 0.0722f;

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
    /// The built-in filters in the order of lists and "next filter": the smart inversion right after
    /// the plain one. The enum order stays as it is, since palette indexes and saved settings depend on it.
    /// </summary>
    public static IReadOnlyList<FilterId> DisplayOrder { get; } =
    [
        FilterId.Inverted, FilterId.InvertedSmart, FilterId.InvertedKeepHue, FilterId.Grayscale,
        FilterId.GrayscaleInverted, FilterId.YellowOnBlack, FilterId.BlackOnYellow, FilterId.WhiteOnBlue,
    ];

    /// <summary>Every list entry: the built-in filters, then the own schemes in place order (M18).</summary>
    public static IReadOnlyList<FilterId> Order(IReadOnlyList<CustomScheme> schemes)
    {
        ArgumentNullException.ThrowIfNull(schemes);
        if (schemes.Count == 0)
        {
            return DisplayOrder;
        }

        var order = new List<FilterId>(DisplayOrder.Count + schemes.Count);
        order.AddRange(DisplayOrder);
        foreach (var scheme in schemes)
        {
            order.Add(scheme.Id);
        }

        return order;
    }

    /// <summary>Position of <paramref name="filter"/> in <paramref name="order"/>; 0 (the first) if missing.</summary>
    public static int IndexIn(IReadOnlyList<FilterId> order, FilterId filter)
    {
        ArgumentNullException.ThrowIfNull(order);
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] == filter)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>The matrix of any filter: own schemes from <paramref name="schemes"/>, a missing one inverts.</summary>
    public static ColorMatrix For(FilterId filter, IReadOnlyList<CustomScheme> schemes)
    {
        ArgumentNullException.ThrowIfNull(schemes);
        return CustomSchemes.IsCustom(filter) && CustomSchemes.Find(schemes, filter) is { } scheme
            ? SchemeMatrices.For(scheme)
            : For(filter);
    }

    /// <summary>The matrix of a built-in filter; an own scheme without its definition inverts.</summary>
    public static ColorMatrix For(FilterId filter) => filter switch
    {
        FilterId.Inverted => s_invert,
        FilterId.InvertedKeepHue => s_hueRotate180.After(s_invert),

        // Where the shader inverts at all (ADR-0024); also what is left when it cannot decide.
        FilterId.InvertedSmart => s_hueRotate180.After(s_invert),
        FilterId.Grayscale => s_luma,
        FilterId.GrayscaleInverted => s_lumaInverted,

        // Inverted luma in red and green only: light text turns yellow, white background black.
        FilterId.YellowOnBlack => new(-Lr, -Lg, -Lb, -Lr, -Lg, -Lb, 0, 0, 0, 1, 1, 0),

        // Luma in red and green only: black text stays black, white background turns yellow.
        FilterId.BlackOnYellow => new(Lr, Lg, Lb, Lr, Lg, Lb, 0, 0, 0, 0, 0, 0),

        // Inverted luma, background (luma 1) becomes dark blue (0, 0, 0.5).
        FilterId.WhiteOnBlue => new(-Lr, -Lg, -Lb, -Lr, -Lg, -Lb, -Lr / 2, -Lg / 2, -Lb / 2, 1, 1, 1),

        // An own scheme whose definition is gone falls back like rules and profiles do (M18).
        _ => s_invert,
    };

    /// <summary>
    /// Filter (if enabled), then contrast around mid-grey and brightness offset.
    /// Contrast 1 and brightness 0 leave colours unchanged.
    /// </summary>
    public static ColorMatrix Build(FilterSettings settings) => Build(settings, CustomSchemes.None);

    /// <inheritdoc cref="Build(FilterSettings)"/>
    public static ColorMatrix Build(FilterSettings settings, IReadOnlyList<CustomScheme> schemes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Adjust(settings).After(settings.Enabled ? For(settings.Filter, schemes) : ColorMatrix.Identity);
    }

    /// <summary>Contrast and brightness only; what windows with "None" get (they still follow both).</summary>
    public static ColorMatrix Adjust(FilterSettings settings)
    {
        var c = (float)settings.Contrast;
        var offset = (0.5f * (1 - c)) + (float)settings.Brightness;
        return new ColorMatrix(c, 0, 0, 0, c, 0, 0, 0, c, offset, offset, offset);
    }
}
