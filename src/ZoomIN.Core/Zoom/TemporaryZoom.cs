namespace ZoomIN.Core.Zoom;

/// <summary>
/// Temporary zoom level per monitor (M17 building block, used by the overview and window drags).
/// A temporary level is a ceiling: the monitor shows <c>min(user target, level)</c>, so 1× lifts
/// the zoom and 2× lowers it only where the user zoomed in further. It never touches
/// <see cref="ZoomLevels"/> or the settings; the render loop glides in and out with the
/// <see cref="ZoomAnimator"/> because it reads <see cref="ZoomModel.EffectiveTargetFor"/>.
/// </summary>
/// <remarks>
/// Rules: only the owner releases its level. A user step that keeps zoom on changes the user
/// target underneath and keeps the level (the monitor shows the new target once released, or at
/// once if it is lower). Explicit user commands clear all levels, so a monitor never stays stuck:
/// zoom on/off, a mode change (<see cref="ZoomModel.Toggle"/>, <see cref="ZoomModel.Activate"/>,
/// <see cref="ZoomModel.SetMode"/>) and a step from or to 1×. Docked mode has no levels: switching
/// to it clears them and <see cref="Set"/> does nothing there (the strip reserves screen space, so
/// 1× would leave an empty reserved area).
/// Owners notice a clear via <see cref="Changed"/> and <see cref="LevelFor"/> returning null.
/// Writers run under the model's lock; the render thread reads an immutable snapshot lock-free.
/// </remarks>
public sealed class TemporaryZoom
{
    private static readonly IReadOnlyDictionary<string, double> s_none = new Dictionary<string, double>();

    private readonly Lock _gate;
    private readonly Func<bool> _docked;
    private readonly Dictionary<(TemporaryZoomOwner Owner, string Monitor), double> _levels = [];
    private IReadOnlyDictionary<string, double> _ceilings = s_none;

    /// <param name="gate">The model's lock; the mode only changes under it.</param>
    /// <param name="docked">True while the model is in Docked mode.</param>
    internal TemporaryZoom(Lock gate, Func<bool> docked)
    {
        _gate = gate;
        _docked = docked;
    }

    /// <summary>
    /// Raised after levels changed, on the thread that caused it. Separate from
    /// <see cref="ZoomModel.Changed"/> because nothing here is saved.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Sets the owner's level on one monitor, clamped to 1×–16×. Setting it again replaces it,
    /// e.g. "zoom in at the frame" first moves the anchor and then releases. Does nothing in
    /// Docked mode (<see cref="LevelFor"/> stays null).
    /// </summary>
    public void Set(TemporaryZoomOwner owner, string monitorId, double level)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        if (!Enum.IsDefined(owner))
        {
            throw new ArgumentOutOfRangeException(nameof(owner), owner, "Unknown owner of a temporary zoom level.");
        }

        if (double.IsNaN(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), "A temporary zoom level must be a number.");
        }

        level = ZoomLimits.Clamp(level);
        bool replaced;
        lock (_gate)
        {
            replaced = _levels.TryGetValue((owner, monitorId), out var old);
            if ((replaced && old == level) || _docked())
            {
                return;
            }

            _levels[(owner, monitorId)] = level;
            PublishLocked();
        }

        // Only new levels count, so every "set" pairs with one "release" or "cleared".
        if (!replaced)
        {
            TemporaryZoomMetrics.Set(owner);
        }

        Changed?.Invoke();
    }

    /// <summary>Releases the owner's level on one monitor; it glides back to the user target.</summary>
    public void Release(TemporaryZoomOwner owner, string monitorId)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        lock (_gate)
        {
            if (!_levels.Remove((owner, monitorId)))
            {
                return;
            }

            PublishLocked();
        }

        TemporaryZoomMetrics.Released(owner, 1);
        Changed?.Invoke();
    }

    /// <summary>Releases the owner's levels on all monitors, e.g. when a drag or the overview ends.</summary>
    public void ReleaseAll(TemporaryZoomOwner owner)
    {
        int removed;
        lock (_gate)
        {
            removed = RemoveWhereLocked(key => key.Owner == owner).Count;
        }

        if (removed > 0)
        {
            TemporaryZoomMetrics.Released(owner, removed);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Moves the owner's level to another monitor in one step (G1: the window crossed monitors).
    /// The old monitor glides back to its own target while the new one glides to the level.
    /// Does nothing if the owner holds no level on <paramref name="fromMonitorId"/>. If it already
    /// holds one on <paramref name="toMonitorId"/>, the lower of both stays.
    /// </summary>
    public void Move(TemporaryZoomOwner owner, string fromMonitorId, string toMonitorId)
    {
        ArgumentNullException.ThrowIfNull(fromMonitorId);
        ArgumentNullException.ThrowIfNull(toMonitorId);
        bool merged;
        lock (_gate)
        {
            if (fromMonitorId == toMonitorId || !_levels.Remove((owner, fromMonitorId), out var level))
            {
                return;
            }

            // The owner may already hold a level there: keep the lower one, as for two owners.
            merged = _levels.TryGetValue((owner, toMonitorId), out var there);
            _levels[(owner, toMonitorId)] = merged ? Math.Min(there, level) : level;
            PublishLocked();
        }

        TemporaryZoomMetrics.Moved(owner);
        if (merged)
        {
            TemporaryZoomMetrics.Released(owner, 1); // two levels became one
        }

        Changed?.Invoke();
    }

    /// <summary>The owner's level on one monitor, or null if it holds none (also after it was cleared).</summary>
    public double? LevelFor(TemporaryZoomOwner owner, string monitorId)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        lock (_gate)
        {
            return _levels.TryGetValue((owner, monitorId), out var level) ? level : null;
        }
    }

    /// <summary>True while any owner holds a level on the monitor. Lock-free.</summary>
    public bool IsActive(string monitorId) => Volatile.Read(ref _ceilings).ContainsKey(monitorId);

    /// <summary>
    /// Lowest level of all owners on the monitor, or 16× if none. Called per frame: lock-free and
    /// without allocation.
    /// </summary>
    public double CeilingFor(string monitorId) => TryGetCeiling(monitorId, out var level) ? level : ZoomLimits.Max;

    /// <summary>The lowest level of all owners on the monitor, from one snapshot. Lock-free, 0 B.</summary>
    public bool TryGetCeiling(string monitorId, out double level) => Volatile.Read(ref _ceilings).TryGetValue(monitorId, out level);

    /// <summary>
    /// Drops the levels on <paramref name="monitors"/> (all monitors if null) for a user command
    /// and returns the owner of each dropped level. Caller holds the lock and passes the result
    /// to <see cref="AnnounceCommand"/> after releasing it.
    /// </summary>
    internal IReadOnlyList<TemporaryZoomOwner> ClearLocked(IReadOnlyCollection<string>? monitors = null) =>
        _levels.Count == 0 ? [] : RemoveWhereLocked(key => monitors is null || monitors.Contains(key.Monitor)).ConvertAll(key => key.Owner);

    /// <summary>
    /// Raises the model's change for a user command, then counts and announces the levels it
    /// cleared. Owners must learn about a clear even if a model handler throws, or they keep
    /// state for a level that is gone.
    /// </summary>
    internal void AnnounceCommand(Action? modelChanged, IReadOnlyList<TemporaryZoomOwner> cleared)
    {
        try
        {
            modelChanged?.Invoke();
        }
        finally
        {
            if (cleared.Count > 0)
            {
                foreach (var owner in cleared)
                {
                    TemporaryZoomMetrics.Cleared(owner);
                }

                Changed?.Invoke();
            }
        }
    }

    private List<(TemporaryZoomOwner Owner, string Monitor)> RemoveWhereLocked(Func<(TemporaryZoomOwner Owner, string Monitor), bool> match)
    {
        var keys = _levels.Keys.Where(match).ToList();
        if (keys.Count > 0)
        {
            keys.ForEach(key => _levels.Remove(key));
            PublishLocked();
        }

        return keys;
    }

    // Changes are rare (key presses, drag start/end), so a fresh snapshot per change is fine.
    private void PublishLocked()
    {
        if (_levels.Count == 0)
        {
            Volatile.Write(ref _ceilings, s_none);
            return;
        }

        var ceilings = new Dictionary<string, double>();
        foreach (var ((_, monitor), level) in _levels)
        {
            ceilings[monitor] = ceilings.TryGetValue(monitor, out var other) ? Math.Min(other, level) : level;
        }

        Volatile.Write(ref _ceilings, ceilings);
    }
}
