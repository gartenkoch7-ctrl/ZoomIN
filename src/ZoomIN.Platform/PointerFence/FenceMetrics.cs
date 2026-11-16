using System.Diagnostics;
using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// Measurements of the pointer fence (docs/kb/telemetrie.md). The mouse hook only increments a
/// number; the SDK reads it through an observable counter, so the hook path stays at 0 B even
/// with a listener.
/// </summary>
internal static class FenceMetrics
{
    private static long s_blocked;

    private static readonly ObservableCounter<long> s_blockedCounter = Telemetry.Meter.CreateObservableCounter(
        "zoomin.pointer.fence_blocked", static () => Volatile.Read(ref s_blocked), unit: "{move}",
        description: "Pointer moves the fence swallowed or clamped");

    private static readonly Counter<long> s_changes = Telemetry.Meter.CreateCounter<long>(
        "zoomin.pointer.fence_changes", unit: "{change}", description: "Fence applied, re-applied or released");

    /// <summary>From the mouse hook: no allocation.</summary>
    public static void Blocked() => Interlocked.Increment(ref s_blocked);

    public static void Changed(FenceMode mode, FenceTrigger trigger)
    {
        // The static field already roots the instrument; this read only tells the analyzer it is used.
        GC.KeepAlive(s_blockedCounter);
        s_changes.Add(
            1,
            new KeyValuePair<string, object?>("zoomin.fence.mode", ModeName(mode)),
            new KeyValuePair<string, object?>("zoomin.fence.trigger", TriggerName(trigger)));
    }

    /// <summary>After the fence was enforced: tags of <c>zoomin.pointer.fence</c> and the change counter.</summary>
    public static void Applied(Activity? activity, FenceMode mode, FenceTrigger trigger, int forbidden)
    {
        activity?.SetTag("zoomin.fence.mode", ModeName(mode));
        activity?.SetTag("zoomin.fence.trigger", TriggerName(trigger));
        activity?.SetTag("zoomin.fence.forbidden", forbidden);
        Changed(mode, trigger);
    }

    public static string ModeName(FenceMode mode) => mode switch
    {
        FenceMode.Clip => "clip",
        FenceMode.Hook => "hook",
        _ => "off",
    };

    public static string TriggerName(FenceTrigger trigger) => trigger switch
    {
        FenceTrigger.DesktopSwitch => "desktop_switch",
        FenceTrigger.SessionUnlock => "session_unlock",
        FenceTrigger.DisplayChange => "display_change",
        FenceTrigger.ClipLost => "clip_lost",
        FenceTrigger.HookLost => "hook_lost",
        FenceTrigger.Dispose => "dispose",
        _ => "request",
    };
}
