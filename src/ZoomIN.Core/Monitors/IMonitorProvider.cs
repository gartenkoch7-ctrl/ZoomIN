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

    /// <summary>Bounds in physical desktop pixels; null if the monitor is gone.</summary>
    PhysicalRect? BoundsOf(string monitorId);

    /// <summary>
    /// Bounds of all current monitors, read in one pass where the platform can (pointer fence, M19);
    /// empty if they cannot all be read.
    /// </summary>
    IReadOnlyList<PhysicalRect> AllBounds()
    {
        var all = new List<PhysicalRect>();
        foreach (var id in MonitorIds())
        {
            if (BoundsOf(id) is not { } bounds)
            {
                return [];
            }

            all.Add(bounds);
        }

        return all;
    }
}
