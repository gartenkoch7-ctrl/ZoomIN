namespace ZoomIN.Core.PointerFence;

/// <summary>Why the fence was (re)computed; part of its logs and of <see cref="IPointerFence.Recomputed"/>.</summary>
public enum FenceTrigger
{
    /// <summary>An owner changed its areas or filters.</summary>
    Request,

    /// <summary>Back from another desktop (Win+L, UAC, Ctrl+Alt+Del): Windows reset ClipCursor.</summary>
    DesktopSwitch,

    /// <summary>The session was unlocked or reconnected.</summary>
    SessionUnlock,

    /// <summary>Monitors were added, removed or rearranged, or the resolution changed.</summary>
    DisplayChange,

    /// <summary>The periodic check found ClipCursor reset by Windows.</summary>
    ClipLost,

    /// <summary>The pointer moved but the hook saw nothing: Windows removed it (too slow); installed again.</summary>
    HookLost,

    /// <summary>The fence is disposed (ZoomIN ends).</summary>
    Dispose,
}
