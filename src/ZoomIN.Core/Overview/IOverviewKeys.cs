using ZoomIN.Core.Hotkeys;

namespace ZoomIN.Core.Overview;

/// <summary>
/// Keyboard parts of the overview (B2) that a registered hotkey cannot do: noticing the release of
/// the shortcut and taking Esc away from the application while the overview runs. Implemented on
/// the low-level keyboard hook; the handler is called on the hook thread and must return quickly.
/// </summary>
public interface IOverviewKeys
{
    /// <summary>
    /// Starts watching for the overview <paramref name="watchId"/>: reports the first release of the
    /// main key or a modifier of <paramref name="gesture"/> (also one that happened before this
    /// call) and swallows Esc until <see cref="Stop"/>.
    /// </summary>
    void Watch(long watchId, HotkeyGesture gesture, IOverviewKeyHandler handler);

    /// <summary>Stops the watch if it is still <paramref name="watchId"/>; Esc reaches applications again. Never throws.</summary>
    void Stop(long watchId);
}
