namespace ZoomIN.Core.Hotkeys;

/// <summary>Registers system-wide shortcuts. Callbacks run on the service's own thread.</summary>
public interface IHotkeyService : IDisposable
{
    /// <param name="gesture">Key combination.</param>
    /// <param name="onPressed">Called on every press.</param>
    /// <param name="repeat">Also call while the key is held (auto-repeat), e.g. for zoom steps.</param>
    /// <returns>False if the gesture is already taken by another application.</returns>
    bool Register(HotkeyGesture gesture, Action onPressed, bool repeat = false);
}
