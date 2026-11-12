using Microsoft.Extensions.Logging;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.App.Freeze;

/// <summary>
/// Runs the freeze controller's steps one at a time (hotkey thread, thread pool). An exception must
/// end neither thread, and a half-done freeze must not keep the pointer out of a monitor: on a
/// failure the fence lets go first, then every freeze is lifted, each step on its own.
/// </summary>
internal sealed class FreezeGuard(IPointerFence fence, MonitorFreeze frozen, FreezeOperations operations, ILogger log)
{
    private readonly Lock _gate = new();

    public void Run(string step, Action action)
    {
        lock (_gate)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                log.FreezeFailed(ex, step);
                Safely(() => fence.Forbid(FenceOwner.FrozenMonitors, []));
                Safely(() => frozen.UnfreezeAll());
                Safely(() => operations.StopAll("failure"));
            }
        }
    }

    private void Safely(Action release)
    {
        try
        {
            release();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The fence releases itself on every failure of its own; nothing more to do here.
            log.FreezeFailed(ex, "release");
        }
    }
}
