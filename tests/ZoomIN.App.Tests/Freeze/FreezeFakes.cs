using ZoomIN.App.Tests.Overview;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.Freeze;

/// <summary>
/// The grilling layout: Windows numbers 1 (left), 3 (middle), 2 (right), 1920×1080 each. Monitors
/// can be unplugged or resized to test display changes.
/// </summary>
internal sealed class ThreeMonitors : IMonitorProvider
{
    public const string One = @"\\.\DISPLAY1";
    public const string Two = @"\\.\DISPLAY2";
    public const string Three = @"\\.\DISPLAY3";

    private readonly Dictionary<string, PhysicalRect> _bounds = new(StringComparer.Ordinal)
    {
        [One] = new PhysicalRect(0, 0, 1920, 1080),
        [Three] = new PhysicalRect(1920, 0, 1920, 1080),
        [Two] = new PhysicalRect(3840, 0, 1920, 1080),
    };

    public static PhysicalPoint MiddleOf(string id) => id switch
    {
        One => new PhysicalPoint(960, 540),
        Three => new PhysicalPoint(2880, 540),
        _ => new PhysicalPoint(4800, 540),
    };

    // Windows lists them by number, not by position.
    public IReadOnlyList<string> MonitorIds() => [.. _bounds.Keys.Order(StringComparer.Ordinal)];

    public string MonitorAt(PhysicalPoint point) => _bounds.FirstOrDefault(pair => pair.Value.Contains(point)).Key ?? One;

    public PhysicalRect? BoundsOf(string monitorId) => _bounds.TryGetValue(monitorId, out var rect) ? rect : null;

    public void Unplug(string id) => _bounds.Remove(id);

    public void Resize(string id, PhysicalRect bounds) => _bounds[id] = bounds;

    public void KeepOnly(string id)
    {
        foreach (var other in _bounds.Keys.Where(key => key != id).ToList())
        {
            _bounds.Remove(other);
        }
    }
}

/// <summary>Records the forbidden areas; the test raises <see cref="Recomputed"/> like the watcher does.</summary>
internal sealed class FakeFence : IPointerFence
{
    public List<IReadOnlyList<PhysicalRect>> Requests { get; } = [];

    public IReadOnlyList<PhysicalRect> Forbidden { get; private set; } = [];

    public bool Throw { get; set; }

    public FenceMode Mode => Forbidden.Count > 0 ? FenceMode.Clip : FenceMode.Off;

    public event EventHandler<FenceTrigger>? Recomputed;

    public void Forbid(FenceOwner owner, IReadOnlyList<PhysicalRect> areas)
    {
        Assert.Equal(FenceOwner.FrozenMonitors, owner);
        if (Throw && areas.Count > 0)
        {
            throw new InvalidOperationException("fence failed");
        }

        Forbidden = [.. areas];
        Requests.Add(Forbidden);
    }

    public void AddFilter(IPointerMoveFilter filter)
    {
    }

    public void RemoveFilter(IPointerMoveFilter filter)
    {
    }

    public void Raise(FenceTrigger trigger) => Recomputed?.Invoke(this, trigger);

    public bool HasListener => Recomputed is not null;

    public void Dispose()
    {
    }
}

/// <summary>Moves the fake cursor like SetCursorPos and checks the fence was not set before the move (ADR-0028).</summary>
internal sealed class FakeMover(MovableCursor cursor, FakeFence fence) : IPointerMover
{
    public List<(PhysicalPoint Target, PointerMoveReason Reason)> Moves { get; } = [];

    public bool Refuse { get; set; }

    public bool MoveTo(PhysicalPoint target, PointerMoveReason reason)
    {
        if (Refuse)
        {
            return false;
        }

        // The pointer must never be moved into an area the fence already keeps it out of.
        Assert.DoesNotContain(fence.Forbidden, area => area.Contains(target));
        Moves.Add((target, reason));
        cursor.Position = target;
        return true;
    }
}
