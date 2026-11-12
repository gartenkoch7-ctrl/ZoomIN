using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.Islands;

/// <summary>A pointer fence that only records its filters; tests raise <see cref="IPointerFence.Recomputed"/>.</summary>
internal sealed class IslandFakeFence : IPointerFence
{
    public event EventHandler<FenceTrigger>? Recomputed;

    public List<IPointerMoveFilter> Filters { get; } = [];

    public FenceMode Mode => Filters.Count > 0 ? FenceMode.Hook : FenceMode.Off;

    public void Forbid(FenceOwner owner, IReadOnlyList<PhysicalRect> areas)
    {
    }

    public void AddFilter(IPointerMoveFilter filter) => Filters.Add(filter);

    public void RemoveFilter(IPointerMoveFilter filter) => Filters.Remove(filter);

    public void Raise(FenceTrigger trigger) => Recomputed?.Invoke(this, trigger);

    public void Dispose()
    {
    }
}
