using Microsoft.Extensions.Logging;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// The fence's ClipCursor rectangle: sets it, notices when Windows dropped or cut it, leaves a
/// clip another program set alone, and clears only a clip of its own. Used under the fence's lock,
/// except <see cref="ReleaseForExit"/>.
/// </summary>
internal sealed class ClipKeeper(ILogger log, IFenceNative native)
{
    // Orders a clip in flight against the process-exit release, which runs without the fence's lock.
    private readonly Lock _exitGate = new();
    private volatile bool _owned;
    private bool _exited;
    private PhysicalRect? _wanted;
    private PhysicalRect? _applied;
    private bool _failureLogged;
    private bool _foreignLogged;
    private bool _reapplyReported;

    /// <summary>True while a rectangle is wanted (it may be briefly not in effect, e.g. on another desktop).</summary>
    public bool IsWanted => _wanted is not null;

    /// <summary>Keeps the pointer in <paramref name="rect"/> (whole pixels), or clears our clip if null.</summary>
    public void Keep(PhysicalRect? rect)
    {
        _wanted = rect;
        if (rect is not { } wanted)
        {
            Clear();
        }
        else if (!TakeForeign())
        {
            TryClip(wanted);
        }
    }

    /// <summary>
    /// The periodic check: true if Windows had dropped or cut the clip and it was set again; once
    /// per stretch, so a program that keeps clipping inside ours is not reported every second.
    /// </summary>
    public bool Check()
    {
        if (_wanted is not { } wanted)
        {
            return false;
        }

        if (native.CurrentClip() == wanted)
        {
            _reapplyReported = false;
            return false;
        }

        if (TakeForeign() || !TryClip(wanted) || _reapplyReported)
        {
            return false;
        }

        _reapplyReported = true;
        return true;
    }

    /// <summary>Clears our clip; another program's stays.</summary>
    public void Clear()
    {
        // Under the exit gate: a process exit meanwhile must see either our clip or none.
        lock (_exitGate)
        {
            _wanted = null;
            UnclipIfOurs();
            _applied = null;
        }
    }

    /// <summary>Process exit: clear without the fence's lock, and never set again afterwards.</summary>
    public void ReleaseForExit()
    {
        // Waits for a ClipCursor call in flight (a quick system call), so it cannot land after the release.
        lock (_exitGate)
        {
            _exited = true;
            UnclipIfOurs();
        }
    }

    // Ownership ends only after the call: if it throws, a later release tries again.
    private void UnclipIfOurs()
    {
        if (!_owned)
        {
            return;
        }

        if (!IsForeign())
        {
            native.Unclip();
        }

        _owned = false;
    }

    // A clip that is neither the free screen nor inside ours belongs to a game or remote tool: not
    // ours to restore or release. Inside ours counts as ours, because Windows cuts our clip to
    // every screen change in between; a foreign clip within ours cannot be told apart and is
    // replaced, which is safer than leaving one of ours behind (the pointer must never stay trapped).
    private bool IsForeign()
    {
        if (native.CurrentClip() is not { } now || now == native.VirtualScreen())
        {
            return false;
        }

        return _applied is not { } mine || !RectMath.Covers(mine, now);
    }

    private bool TakeForeign()
    {
        lock (_exitGate)
        {
            if (!IsForeign())
            {
                return false;
            }

            _owned = false;
            _applied = null;
        }

        if (!_foreignLogged)
        {
            _foreignLogged = true;
            log.ForeignClip();
        }

        return true;
    }

    private bool TryClip(PhysicalRect rect)
    {
        int error;
        lock (_exitGate)
        {
            if (_exited)
            {
                return false;
            }

            // Marked first: whatever happens, a later release clears it.
            _owned = true;
            if (native.ClipTo(rect, out error))
            {
                _applied = rect;
                _failureLogged = false;
                _foreignLogged = false;
                return true;
            }
        }

        if (!_failureLogged)
        {
            _failureLogged = true;
            log.ClipFailed(error);
        }

        return false;
    }
}
