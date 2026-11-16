namespace ZoomIN.Core.Filters;

/// <summary>
/// Which colour filter: the eight built-in ones of M5/M13 plus four places for the user's own
/// schemes (M18, C1–C3). Own values, no third-party palettes (AGENTS.md 3). Saved by name, so the
/// names of the built-in filters must never change (settings of older versions keep loading).
/// </summary>
public enum FilterId
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
    /// original. Decided per pixel on the GPU; <see cref="ColorFilters.For(FilterId)"/> gives the inverted part.
    /// </summary>
    InvertedSmart,

    /// <summary>
    /// Places of the user's own schemes (<see cref="CustomScheme"/>). A place keeps its number when
    /// another scheme is deleted, so rules and profiles that name it stay right. After the built-in
    /// filters, so their palette indexes and saved values stay put.
    /// </summary>
    Custom1,

    Custom2,

    Custom3,

    Custom4,
}
