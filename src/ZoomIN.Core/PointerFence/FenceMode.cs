namespace ZoomIN.Core.PointerFence;

/// <summary>How the fence currently keeps the pointer out.</summary>
public enum FenceMode
{
    /// <summary>Nothing is confined; no hook, no ClipCursor.</summary>
    Off,

    /// <summary>The allowed area is one rectangle: ClipCursor.</summary>
    Clip,

    /// <summary>Another shape or move filters: a low-level mouse hook that swallows moves.</summary>
    Hook,
}
