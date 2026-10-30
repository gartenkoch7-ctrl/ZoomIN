using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Modes;

/// <summary>
/// Reserves a strip of the screen like the taskbar does, so maximized windows fit into the rest
/// (docked mode, M4 decision). Safe to call from any thread.
/// </summary>
public interface IScreenReservation : IDisposable
{
    /// <param name="area">Strip in physical desktop pixels.</param>
    /// <param name="edge">Monitor edge the strip is attached to.</param>
    void Reserve(PhysicalRect area, DockEdge edge);

    /// <summary>Gives the space back. No-op when nothing is reserved.</summary>
    void Release();
}
