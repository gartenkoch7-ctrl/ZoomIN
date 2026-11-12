using Microsoft.Extensions.Logging;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// The fence's check about once a second (watcher thread, under the fence's lock): reports filter
/// failures the hook could not log, sets a clip Windows dropped again, and reinstalls a hook
/// Windows removed silently.
/// </summary>
internal sealed class FenceChecker(ILogger log, IFenceNative native, FenceEnforcer enforcer, HookHeartbeat heartbeat)
{
    /// <returns>True if the fence ended up off (a new hook was refused).</returns>
    public bool Run(IFenceHookTarget target, int areas, int filters)
    {
        var failures = heartbeat.TakeFailures();
        if (failures > 0)
        {
            log.FilterFailures(failures);
        }

        if (enforcer.CheckClip())
        {
            FenceMetrics.Changed(enforcer.Mode, FenceTrigger.ClipLost);
            log.FenceChanged(enforcer.Mode, FenceTrigger.ClipLost, areas, filters);
        }

        if (enforcer.Mode != FenceMode.Hook || !heartbeat.IsSilent(native.CursorPosition()))
        {
            return false;
        }

        heartbeat.Reinstalled();
        var installed = enforcer.ReinstallHook(target);
        FenceMetrics.Changed(enforcer.Mode, FenceTrigger.HookLost);
        log.FenceChanged(enforcer.Mode, FenceTrigger.HookLost, areas, filters);
        return !installed;
    }
}
