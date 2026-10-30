using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Current mouse position in physical pixels. Called once per frame from every render thread,
/// so implementations must be thread-safe, fast and allocation-free.
/// </summary>
public interface ICursorSource
{
    /// <returns>False if the position is unavailable (e.g. secure desktop); callers keep their last value.</returns>
    bool TryGetPosition(out PhysicalPoint position);
}
