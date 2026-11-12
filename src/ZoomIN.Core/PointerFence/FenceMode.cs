namespace ZoomIN.Core.PointerFence;

/// <summary>How the fence currently keeps the pointer out.</summary>
public enum FenceMode
{
    /// <summary>Nothing is confined; no hook, no ClipCursor.</summary>
    Off,

    /// <summary>
    /// The allowed area is one rectangle: ClipCursor. While another desktop is active Windows
    /// refuses it (log 2944); the mode stays, and the fence sets the clip again on return.
    /// </summary>
    Clip,

    /// <summary>
    /// Another shape or move filters: a low-level mouse hook that swallows moves. If the allowed
    /// area is one rectangle, ClipCursor holds it as well.
    /// </summary>
    Hook,
}
