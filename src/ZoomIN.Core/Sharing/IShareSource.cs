using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Sharing;

/// <summary>
/// Watches the meeting app for an active share and keeps a window marked by hotkey up to date;
/// publishes both to a <see cref="ShareModel"/> from its own thread.
/// </summary>
public interface IShareSource : IDisposable
{
    void Start(ShareModel model);

    /// <summary>
    /// Top-level window under the point (not ZoomIN's own) with its bounds; false over the desktop
    /// or taskbar. The id is opaque to Core (a window handle on Windows).
    /// </summary>
    bool TryGetWindowAt(PhysicalPoint point, out long windowId, out PhysicalRect bounds);
}
