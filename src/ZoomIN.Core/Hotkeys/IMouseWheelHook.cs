namespace ZoomIN.Core.Hotkeys;

/// <summary>
/// Global mouse-wheel gesture (default Strg+Mausrad). Matching wheel events are swallowed, so
/// ZoomIN takes precedence over the zoom of the focused application (ADR-0008).
/// </summary>
public interface IMouseWheelHook : IDisposable
{
    /// <param name="modifiers">Exactly these modifiers must be held.</param>
    /// <param name="onNotches">Wheel movement in notches (+ = away from the user); runs on the hook thread.</param>
    void Start(HotkeyModifiers modifiers, Action<double> onNotches);
}
