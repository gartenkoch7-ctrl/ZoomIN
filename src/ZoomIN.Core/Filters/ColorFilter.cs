namespace ZoomIN.Core.Filters;

/// <summary>Colour filters of the MVP (M5 decision). Own values, no third-party palettes (AGENTS.md 3).</summary>
public enum ColorFilter
{
    /// <summary>White becomes black, colours are inverted.</summary>
    Inverted,

    /// <summary>Only brightness is inverted; red stays red, so photos and icons stay recognisable.</summary>
    InvertedKeepHue,

    Grayscale,

    GrayscaleInverted,

    /// <summary>Light text on black background, coloured yellow.</summary>
    YellowOnBlack,

    /// <summary>Dark text on yellow background.</summary>
    BlackOnYellow,

    /// <summary>White text on dark blue background.</summary>
    WhiteOnBlue,

    /// <summary>
    /// Inverts only light surroundings, keeping the hue (ADR-0024): dark bars stay dark, photos stay
    /// original. Decided per pixel on the GPU; <see cref="ColorFilters.For"/> gives the inverted part.
    /// Last in the enum, so settings and palette indexes of the other filters stay put.
    /// </summary>
    InvertedSmart,
}
