using ZoomIN.Core.Filters;

namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// What a window region shows, as an index into a small set of matrices that the shader gets
/// once per settings change: the monitor's own filter, original colours, or one of the filters.
/// Contrast and brightness are part of every entry ("for everything", ADR-0020).
/// </summary>
public static class WindowPalette
{
    /// <summary>Like the rest of the monitor: its filter if it is on, otherwise original colours.</summary>
    public const byte Monitor = 0;

    /// <summary>Original colours, also while the monitor is filtered.</summary>
    public const byte Original = 1;

    /// <summary>
    /// Number of entries: monitor, original, the 8 built-in filters and the 4 places of own schemes
    /// (M18). Fixed, so an index means the same whatever schemes exist; PALETTE_SIZE in Magnify.hlsl.
    /// </summary>
    public const int Count = 2 + FilterCount;

    /// <summary>The entry of the first own scheme (<see cref="FilterId.Custom1"/>); FIRST_SCHEME_ENTRY in Magnify.hlsl.</summary>
    public const byte FirstScheme = 2 + (int)FilterId.Custom1;

    private const int FilterCount = (int)FilterId.Custom4 + 1;

    public static byte For(FilterId filter) => (byte)(2 + (int)filter);

    /// <summary>
    /// The smart inversion (ADR-0024): its matrix is the inverted part only; the shader blends it
    /// per pixel with <see cref="Original"/>. Same matrix as "keep hue", so compare by index too.
    /// </summary>
    public static byte Smart { get; } = For(FilterId.InvertedSmart);

    /// <summary>True for entries that are a filter of their own (the window is left out of the monitor filter).</summary>
    public static bool IsOwnFilter(byte index) => index >= 2;

    /// <summary>The place (0–3) of the own scheme the entry shows, or −1 (M18).</summary>
    public static int SchemeSlotOf(byte index) =>
        index is >= FirstScheme and < Count ? index - FirstScheme : -1;

    /// <summary>Technical name of an entry for diagnostics: "monitor filter", "original" or the filter.</summary>
    public static string Name(byte index) => index switch
    {
        Monitor => "monitor filter",
        Original => "original",
        _ => ((FilterId)(index - 2)).ToString(),
    };

    public static ColorMatrix[] Build(FilterSettings shared) => Build(shared, CustomSchemes.None);

    /// <summary>All <see cref="Count"/> matrices; places without a scheme invert, like a deleted one.</summary>
    public static ColorMatrix[] Build(FilterSettings shared, IReadOnlyList<CustomScheme> schemes)
    {
        var adjust = ColorFilters.Adjust(shared);
        var matrices = new ColorMatrix[Count];
        matrices[Monitor] = ColorFilters.Build(shared, schemes);
        matrices[Original] = adjust;
        for (var filter = FilterId.Inverted; filter <= FilterId.Custom4; filter++)
        {
            matrices[For(filter)] = adjust.After(ColorFilters.For(filter, schemes));
        }

        return matrices;
    }
}
