using Microsoft.Extensions.Logging;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Shell;

namespace ZoomIN.App.Freeze;

/// <summary>
/// Lifts freezes (G2) for the shortcut, fence events, a pointer seen inside and the exit. Unfreezes
/// first, then frees the area: the monitor follows the pointer again before it can get there. Called
/// inside <see cref="FreezeGuard"/>: if a listener of the thaw fails (e.g. saving the settings), the
/// guard also frees every area and lifts every freeze.
/// </summary>
internal sealed class FreezeLifter(
    MonitorFreeze frozen, IPointerFence fence, FreezeOperations operations, IAnnouncer announcer, ILocalizer texts, ILogger log)
{
    /// <param name="restoreLevel">False after a display change: the monitor is gone or changed and thaws at 1×.</param>
    public void Lift(Func<FrozenMonitor, bool> which, string reason, bool announce = true, bool restoreLevel = true)
    {
        IReadOnlyList<FrozenMonitor> removed;
        try
        {
            removed = frozen.Unfreeze(which, restoreLevel);
        }
        catch
        {
            // A listener of the thaw failed: the thawed area is free at once, whoever called (the guard then
            // frees and lifts the rest). A failing fence is only logged, so the listener's failure is reported.
            FreeRemaining();
            throw;
        }

        if (removed.Count == 0)
        {
            return;
        }

        fence.Forbid(FenceOwner.FrozenMonitors, frozen.State.Areas());
        operations.Stop(removed, reason);
        log.FreezeLifted(removed.Count, reason);
        if (announce)
        {
            announcer.Announce(texts.Get("Freeze.Lifted"));
        }
    }

    private void FreeRemaining()
    {
        try
        {
            fence.Forbid(FenceOwner.FrozenMonitors, frozen.State.Areas());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.FreezeFailed(ex, "release");
        }
    }
}
