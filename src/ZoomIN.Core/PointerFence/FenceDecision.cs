using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.PointerFence;

/// <summary>
/// What the mouse hook decides per move: the filters in their order, then the forbidden areas,
/// which therefore always win. Immutable; the fence swaps the whole object when something changes,
/// and <see cref="Decide"/> does not allocate.
/// </summary>
public sealed class FenceDecision
{
    private readonly IPointerMoveFilter[] _filters;

    /// <param name="geometry">Allowed area; ignored unless it restricts and leaves something allowed.</param>
    /// <param name="filters">Move filters in any order; they run sorted by <see cref="IPointerMoveFilter.Order"/>.</param>
    public FenceDecision(FenceGeometry? geometry, IReadOnlyList<IPointerMoveFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        Geometry = geometry is { IsRestricted: true, IsEmpty: false } ? geometry : null;
        _filters = [.. filters.OrderBy(filter => filter.Order)];
        Mode = _filters.Length > 0 || Geometry is { SingleRect: null } ? FenceMode.Hook
            : Geometry is not null ? FenceMode.Clip
            : FenceMode.Off;
    }

    /// <summary>Lets every move through.</summary>
    public static FenceDecision Open { get; } = new(null, []);

    /// <summary>The restricting area; null if nothing is forbidden, or everything (never enforced: no trap).</summary>
    public FenceGeometry? Geometry { get; }

    /// <summary>How this decision has to be enforced.</summary>
    public FenceMode Mode { get; }

    public int FilterCount => _filters.Length;

    /// <summary>Where a move from <paramref name="current"/> to <paramref name="requested"/> may end.</summary>
    public PhysicalPoint Decide(long timestamp, PhysicalPoint current, PhysicalPoint requested)
    {
        var target = requested;
        for (var i = 0; i < _filters.Length; i++)
        {
            target = _filters[i].Filter(timestamp, current, target);
        }

        return Geometry is null ? target : Geometry.Constrain(current, target);
    }
}
