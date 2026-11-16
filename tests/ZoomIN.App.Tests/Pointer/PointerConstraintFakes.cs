using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.Pointer;

/// <summary>A fence that only records filters and raises recomputes on demand.</summary>
internal sealed class FakeFence : IPointerFence
{
    public event EventHandler<FenceTrigger>? Recomputed;

    public List<IPointerMoveFilter> Filters { get; } = [];

    public bool FailNextAdd { get; set; }

    public FenceMode Mode => Filters.Count > 0 ? FenceMode.Hook : FenceMode.Off;

    public void Forbid(FenceOwner owner, IReadOnlyList<PhysicalRect> areas)
    {
    }

    public void AddFilter(IPointerMoveFilter filter)
    {
        if (FailNextAdd)
        {
            FailNextAdd = false;
            throw new InvalidOperationException("hook refused");
        }

        Filters.Add(filter);
    }

    public void RemoveFilter(IPointerMoveFilter filter) => Filters.Remove(filter);

    public void Raise(FenceTrigger trigger) => Recomputed?.Invoke(this, trigger);

    public void Dispose()
    {
    }
}

/// <summary>A key watch the test presses by hand.</summary>
internal sealed class FakeKeys : IHoldKeyWatch
{
    private Action<bool>? _changed;

    public AxisLockKey? Key { get; private set; }

    public int Starts { get; private set; }

    public int Resets { get; private set; }

    /// <summary>A watch that misses the release on Stop.</summary>
    public bool Silent { get; set; }

    public bool FailNextStop { get; set; }

    public void Start(AxisLockKey key, Action<bool> changed)
    {
        Key = key;
        _changed = changed;
        Starts++;
    }

    public void Reset()
    {
        Resets++;
        _changed?.Invoke(false);
    }

    public void Stop()
    {
        if (FailNextStop)
        {
            FailNextStop = false;
            throw new InvalidOperationException("hook thread stuck");
        }

        if (!Silent)
        {
            _changed?.Invoke(false);
        }

        Key = null;
    }

    public void Press(bool held) => _changed?.Invoke(held);

    public void Dispose() => Stop();
}
