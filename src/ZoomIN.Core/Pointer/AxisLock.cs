using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Pointer;

/// <summary>
/// Axis lock (A6): while the user holds the chosen key, the pointer moves only horizontally or
/// only vertically, e.g. along a table row. The first <see cref="Threshold"/> pixels after the
/// press move freely; then the larger axis wins (a tie goes to horizontal, the usual reading
/// direction) and the pointer stays on the row or column where the key was pressed. Releasing the
/// key frees it at once. A move filter of the pointer fence: it runs inside the low-level mouse
/// hook, only computes and never allocates.
/// </summary>
/// <remarks>
/// <see cref="SetHeld"/> comes from the keyboard hook thread and only sets a flag; the lock state
/// is only touched by <see cref="Filter"/>, i.e. by the mouse hook thread.
/// </remarks>
public sealed class AxisLock : IPointerMoveFilter
{
    /// <summary>Movement (physical pixels, along either axis) after which the axis is chosen.</summary>
    public const double Threshold = 8;

    private volatile bool _held;

    // Counts presses, so a release and press between two moves still starts a new lock.
    private int _press;

    // Mouse hook thread only.
    private bool _active;
    private int _activePress;
    private PhysicalPoint _anchor;
    private PhysicalPoint _last;
    private LockAxis _axis;

    public AxisLock() => PointerConstraintMetrics.EnsureCreated();

    public int Order => PointerFilterOrder.AxisLock;

    public bool IsHeld => _held;

    /// <summary>The chosen axis while the key is held (for tests and diagnostics).</summary>
    public LockAxis Axis => _active && _held ? _axis : LockAxis.None;

    /// <summary>The key went down (true) or up (false). Allocation-free; safe from any thread.</summary>
    public void SetHeld(bool held)
    {
        if (held)
        {
            Interlocked.Increment(ref _press);
        }

        _held = held;
    }

    public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested)
    {
        if (!_held)
        {
            _active = false;
            return requested;
        }

        var press = Volatile.Read(ref _press);
        if (!_active || press != _activePress)
        {
            _active = true;
            _activePress = press;
            _anchor = current;
            _last = current;
            _axis = LockAxis.None;
        }

        // Not where this filter let it go: something moved the pointer past the hook (a shortcut's
        // SetCursorPos, another program) or a later filter clamped it. Measure and lock from
        // there instead of pulling it back. Locked, only the line's coordinate matters.
        var moved = _axis switch
        {
            LockAxis.Horizontal => Math.Abs(current.Y - _anchor.Y) > 1,
            LockAxis.Vertical => Math.Abs(current.X - _anchor.X) > 1,
            _ => Math.Abs(current.X - _last.X) > 1 || Math.Abs(current.Y - _last.Y) > 1,
        };
        if (moved)
        {
            _anchor = current;
        }

        if (_axis == LockAxis.None)
        {
            var dx = Math.Abs(requested.X - _anchor.X);
            var dy = Math.Abs(requested.Y - _anchor.Y);
            if (Math.Max(dx, dy) < Threshold)
            {
                _last = requested;
                return requested;
            }

            _axis = dx >= dy ? LockAxis.Horizontal : LockAxis.Vertical;
            PointerConstraintMetrics.Locked(_axis);
        }

        return _axis == LockAxis.Horizontal
            ? new PhysicalPoint(requested.X, _anchor.Y)
            : new PhysicalPoint(_anchor.X, requested.Y);
    }
}
