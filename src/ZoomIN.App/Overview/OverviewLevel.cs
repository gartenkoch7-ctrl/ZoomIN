using ZoomIN.Core.Overview;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Overview;

/// <summary>
/// The overview's temporary 1× (B2): set and given back so that a start and an older end on
/// another thread never undo each other. Its own lock is never taken while holding the
/// controller's: <see cref="TemporaryZoom.Set"/> and <see cref="TemporaryZoom.Release"/> raise
/// <see cref="TemporaryZoom.Changed"/>, whose handler in the controller takes that lock.
/// </summary>
internal sealed class OverviewLevel(ZoomModel zoom, OverviewModel overview)
{
    private const TemporaryZoomOwner Owner = TemporaryZoomOwner.Overview;

    private readonly Lock _gate = new();

    /// <summary>
    /// Sets 1× on the overview's monitor unless it already ended on another thread. Checked with
    /// the Set in one step, so an end before it never sees a 1× flicker. False if it had ended.
    /// </summary>
    public bool TrySet(OverviewSession session)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(overview.Current, session))
            {
                return false;
            }

            zoom.Temporary.Set(Owner, session.MonitorId, ZoomLimits.Min);
            return true;
        }
    }

    /// <summary>
    /// Gives the 1× back unless a newer overview runs on the same monitor (it keeps the level).
    /// Check and release in one step, so the next overview's Set never lands between them.
    /// </summary>
    public void ReleaseUnlessNewer(OverviewSession session)
    {
        lock (_gate)
        {
            if (overview.Current?.MonitorId != session.MonitorId)
            {
                zoom.Temporary.Release(Owner, session.MonitorId);
            }
        }
    }

    /// <summary>
    /// True while the overview's level on the monitor is still set (a user command clears it).
    /// Must not take <c>_gate</c>: the controller calls it under its own lock.
    /// </summary>
    public bool IsHeld(string monitorId) => zoom.Temporary.LevelFor(Owner, monitorId) is not null;
}
