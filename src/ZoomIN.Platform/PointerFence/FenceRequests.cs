using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.PointerFence;

/// <summary>What the owners asked for: forbidden areas per owner and the move filters. Used under the fence's lock.</summary>
internal sealed class FenceRequests
{
    private readonly Dictionary<FenceOwner, PhysicalRect[]> _forbidden = [];
    private readonly List<IPointerMoveFilter> _filters = [];

    public IReadOnlyList<IPointerMoveFilter> Filters => _filters;

    /// <summary>True while any owner has an area or a filter: the fence must keep watching.</summary>
    public bool Any => _forbidden.Count > 0 || _filters.Count > 0;

    public void Forbid(FenceOwner owner, PhysicalRect[] areas)
    {
        if (areas.Length == 0)
        {
            _forbidden.Remove(owner);
        }
        else
        {
            _forbidden[owner] = areas;
        }
    }

    public void Add(IPointerMoveFilter filter)
    {
        if (!_filters.Contains(filter))
        {
            _filters.Add(filter);
        }
    }

    public void Remove(IPointerMoveFilter filter) => _filters.Remove(filter);

    public PhysicalRect[] Areas() => [.. _forbidden.Values.SelectMany(rects => rects)];
}
