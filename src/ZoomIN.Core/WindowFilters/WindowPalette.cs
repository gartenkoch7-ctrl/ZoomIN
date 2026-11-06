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

    private static readonly ColorFilter[] s_filters = Enum.GetValues<ColorFilter>();

    /// <summary>Number of entries: monitor, original and one per filter.</summary>
    public static int Count { get; } = 2 + s_filters.Length;

    public static byte For(ColorFilter filter) => (byte)(2 + (int)filter);

    /// <summary>
    /// The smart inversion (ADR-0024): its matrix is the inverted part only; the shader blends it
    /// per pixel with <see cref="Original"/>. Same matrix as "keep hue", so compare by index too.
    /// </summary>
    public static byte Smart { get; } = For(ColorFilter.InvertedSmart);

    /// <summary>True for entries that are a filter of their own (the window is left out of the monitor filter).</summary>
    public static bool IsOwnFilter(byte index) => index >= 2;

    public static ColorMatrix[] Build(FilterSettings shared)
    {
        var adjust = ColorFilters.Adjust(shared);
        var matrices = new ColorMatrix[Count];
        matrices[Monitor] = ColorFilters.Build(shared);
        matrices[Original] = adjust;
        foreach (var filter in s_filters)
        {
            matrices[For(filter)] = adjust.After(ColorFilters.For(filter));
        }

        return matrices;
    }
}
