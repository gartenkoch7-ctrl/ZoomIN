using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Current mouse position in physical pixels. Called once per frame from the render thread,
/// so implementations must be fast and allocation-free.
/// </summary>
public interface ICursorSource
{
    PhysicalPoint GetPosition();
}
