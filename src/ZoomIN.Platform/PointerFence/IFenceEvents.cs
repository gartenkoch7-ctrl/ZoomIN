using ZoomIN.Core.PointerFence;

namespace ZoomIN.Platform.PointerFence;

/// <summary>Receives the watcher's events on its thread.</summary>
internal interface IFenceEvents
{
    /// <summary>Desktop switch, session unlock or display change: re-apply.</summary>
    void OnTrigger(FenceTrigger trigger);

    /// <summary>About once a second while the fence is on: ClipCursor still ours? Filter failures to report?</summary>
    void OnCheck();
}
