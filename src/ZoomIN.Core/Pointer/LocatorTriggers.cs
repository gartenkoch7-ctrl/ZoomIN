namespace ZoomIN.Core.Pointer;

/// <summary>What shows the pointer locator ring (defaults: hotkey, shake, after idle).</summary>
[Flags]
public enum LocatorTriggers
{
    None = 0,

    /// <summary>Strg+Alt+S.</summary>
    Hotkey = 1,

    /// <summary>Quick back-and-forth movement of the mouse.</summary>
    Shake = 2,

    /// <summary>The mouse moves again after more than 3 s of rest.</summary>
    AfterIdle = 4,

    /// <summary>Ctrl pressed and released on its own.</summary>
    CtrlKey = 8,
}
