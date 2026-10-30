using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Monitors;

/// <summary>
/// Current monitors, identified by their Windows device name (e.g. "\\.\DISPLAY1"), which is
/// also what DXGI reports, so Platform and Rendering agree on the id. Cheap enough to call per command.
/// </summary>
public interface IMonitorProvider
{
    IReadOnlyList<string> MonitorIds();

    /// <summary>Monitor containing the point, or the nearest one.</summary>
    string MonitorAt(PhysicalPoint point);
}
