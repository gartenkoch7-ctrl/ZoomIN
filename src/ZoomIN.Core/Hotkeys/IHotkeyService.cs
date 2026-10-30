namespace ZoomIN.Core.Hotkeys;

/// <summary>Registers system-wide shortcuts. Callbacks run on the service's own thread.</summary>
public interface IHotkeyService : IDisposable
{
    /// <returns>False if the gesture is already taken by another application.</returns>
    bool Register(HotkeyGesture gesture, Action onPressed);
}
