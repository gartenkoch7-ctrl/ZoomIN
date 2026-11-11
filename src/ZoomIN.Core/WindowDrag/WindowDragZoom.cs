using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.WindowDrag;

/// <summary>
/// Zoom while moving windows (G1): holds the temporary level of <see cref="WindowDragPolicy"/> on
/// the monitor under the pointer from drag start to drag end, moves it along when the pointer
/// changes monitors, and gives it back at the end. The return glides with the normal animation at
/// the mouse position. Nothing here is saved; the options are, per profile.
/// </summary>
/// <remarks>
/// Calls come from the drag source's thread. User commands clear temporary levels from other
/// threads; instead of listening to <see cref="TemporaryZoom.Changed"/> (whose handler would wait
/// for this lock while other owners hold theirs: lock-order inversion), every call first checks
/// whether its level is still there. Lock order: this lock, then the model's lock.
/// </remarks>
public sealed class WindowDragZoom : IDisposable
{
    private const TemporaryZoomOwner Owner = TemporaryZoomOwner.WindowDrag;

    private readonly ZoomModel _model;
    private readonly Lock _gate = new();
    private WindowDragOptions _options;

    // The drag in progress, guarded by _gate.
    private WindowDragOptions _dragOptions = WindowDragOptions.Default;
    private bool _dragging;
    private WindowDragKind _kind;
    private string _monitor = string.Empty;
    private double _levelBefore;
    private double? _level;
    private double _ceiling;
    private bool _held;
    private bool _crossed;
    private bool _cleared;

    public WindowDragZoom(ZoomModel model, WindowDragOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _model = model;
        _options = options.Normalized();
    }

    /// <summary>Raised after <see cref="Options"/> changed, so the settings are saved.</summary>
    public event Action? OptionsChanged;

    /// <summary>The settings; a change applies from the next drag on.</summary>
    public WindowDragOptions Options
    {
        get => Volatile.Read(ref _options);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Volatile.Write(ref _options, value.Normalized());
            OptionsChanged?.Invoke();
        }
    }

    /// <summary>
    /// True if a drag could change the zoom now: the options act, zoom is on and the mode is not
    /// Docked. Lock-free; the drag source asks it at every move/size start and skips probing otherwise.
    /// </summary>
    public bool CanAct => WindowDragPolicy.CanAct(_model.Mode, _model.IsOn, Options);

    /// <summary>
    /// Changes the options from their current value in one atomic step, so a profile switch on
    /// another thread at the same moment is not overwritten with older values.
    /// </summary>
    public void Update(Func<WindowDragOptions, WindowDragOptions> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        WindowDragOptions current, next;
        do
        {
            current = Volatile.Read(ref _options);
            next = change(current).Normalized();
            if (next == current)
            {
                return; // nothing changed: no save
            }
        }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref _options, next, current), current)); // records compare by value

        OptionsChanged?.Invoke();
    }

    /// <summary>A drag began (the window changed) with the pointer on <paramref name="monitorId"/>.</summary>
    public void Start(WindowDragKind kind, string monitorId)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        lock (_gate)
        {
            // A second start without an end (a missed event): the old drag is over.
            ReleaseLocked();
            _dragOptions = Options;
            _dragging = true;
            _kind = kind;
            _monitor = monitorId;
            _levelBefore = _model.TargetFor(monitorId);
            _level = null;
            _crossed = false;
            _cleared = false;

            // The level is set only here. The source skips drags that cannot act (CanAct, at the
            // press); this check covers the rest, e.g. a kind whose switch is off or a change since
            // the press. Without a level the drag leaves the zoom alone to its end; once cleared by
            // a command, it is not set again either.
            if (_model.IsOn && WindowDragPolicy.LevelFor(kind, _model.Mode, _dragOptions) is { } level)
            {
                HoldLocked(level);
            }
        }
    }

    /// <summary>
    /// The pointer is on <paramref name="monitorId"/> now. On another monitor, that one gets the
    /// level and the old one glides back to its own level.
    /// </summary>
    public void PointerOn(string monitorId)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        lock (_gate)
        {
            if (!_dragging || monitorId == _monitor)
            {
                return;
            }

            RefreshLocked();
            var from = _monitor;
            _monitor = monitorId;
            _crossed = true;
            if (_held)
            {
                try
                {
                    _model.Temporary.Move(Owner, from, monitorId);
                }
                finally
                {
                    RefreshLocked(); // a command may have cleared it just before the move
                }

                NoteLoweredLocked();
            }
        }
    }

    /// <summary>
    /// The monitor set changed during the drag: levels hang on monitor ids, so give them back and
    /// leave this drag alone. True if a level was given back.
    /// </summary>
    public bool MonitorsChanged()
    {
        lock (_gate)
        {
            RefreshLocked();
            if (!_dragging || !_held)
            {
                return false;
            }

            _cleared = true;
            ReleaseLocked();
            return true;
        }
    }

    /// <summary>The drag ended: the level is given back. Null if no drag was in progress.</summary>
    public WindowDragReport? End()
    {
        lock (_gate)
        {
            if (!_dragging)
            {
                return null;
            }

            _dragging = false;
            RefreshLocked();
            NoteLoweredLocked(); // the user may have zoomed in above the ceiling meanwhile
            ReleaseLocked();
            return new WindowDragReport(_kind, _dragOptions.Action, _monitor, _levelBefore, _level, _crossed, _cleared);
        }
    }

    /// <summary>Gives back a level still held (shutdown during a drag).</summary>
    public void Dispose() => End();

    private void HoldLocked(double level)
    {
        _ceiling = level;
        try
        {
            _model.Temporary.Set(Owner, _monitor, level);
        }
        finally
        {
            // Checked even if a change handler threw: a stored level must be given back at the end.
            // Gone already: a user command cleared it right after Set, or the mode became Docked,
            // where Set does nothing. Either way the user's command wins for the rest of the drag.
            _held = true;
            RefreshLocked();
        }

        NoteLoweredLocked();
    }

    // User commands (zoom on/off, mode change, a step from or to 1×) clear levels: notice it, so
    // nothing is moved that is gone, the level is not set again, and the report says so.
    private void RefreshLocked()
    {
        if (_held && _model.Temporary.LevelFor(Owner, _monitor) is null)
        {
            _held = false;
            _cleared = true;
        }
    }

    // For the report: the level counts once a monitor really shows it lower than the user's.
    private void NoteLoweredLocked()
    {
        if (_held && _level is null && _ceiling < _model.TargetFor(_monitor))
        {
            _level = _ceiling;
        }
    }

    private void ReleaseLocked()
    {
        if (_held)
        {
            _held = false;
            _model.Temporary.ReleaseAll(Owner);
        }
    }
}
