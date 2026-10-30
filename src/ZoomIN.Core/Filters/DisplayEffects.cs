namespace ZoomIN.Core.Filters;

/// <summary>Immutable snapshot of everything that changes how pixels look; read per frame.</summary>
public sealed record DisplayEffects
{
    private readonly FilterSettings _filter = FilterSettings.Default;

    public DisplayEffects(FilterSettings filter, Smoothing smoothing, CursorStyle cursor)
    {
        Filter = filter;
        Smoothing = smoothing;
        Cursor = cursor;
    }

    public static DisplayEffects Default { get; } = new(FilterSettings.Default, Smoothing.Text, CursorStyle.Default);

    /// <summary>
    /// Setting the filter (also via <c>with</c>) recomputes <see cref="Matrix"/>; a plain
    /// auto-property would copy the old matrix in <c>with</c> expressions.
    /// </summary>
    public FilterSettings Filter
    {
        get => _filter;
        init
        {
            _filter = value;
            Matrix = ColorFilters.Build(value);
        }
    }

    public Smoothing Smoothing { get; init; }

    public CursorStyle Cursor { get; init; }

    /// <summary>Precomputed, so the render thread never builds matrices.</summary>
    public ColorMatrix Matrix { get; private init; } = ColorMatrix.Identity;
}
