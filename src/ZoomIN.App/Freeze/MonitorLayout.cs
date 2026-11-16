using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Freeze;

/// <summary>The current monitors as parallel arrays for <see cref="MonitorCycle"/>, read once per command.</summary>
internal sealed class MonitorLayout
{
    private MonitorLayout(string[] ids, PhysicalRect[] bounds)
    {
        Ids = ids;
        Bounds = bounds;
    }

    public string[] Ids { get; }

    public PhysicalRect[] Bounds { get; }

    public int Count => Ids.Length;

    /// <summary>
    /// Monitors with their bounds from one enumeration (no mix of two display states); empty if
    /// Windows cannot describe one of them, e.g. in the middle of a display change.
    /// </summary>
    public static MonitorLayout Read(IMonitorProvider monitors)
    {
        var all = monitors.AllMonitors().Where(m => m.Bounds is { Width: > 0, Height: > 0 }).ToList();
        return new MonitorLayout([.. all.Select(m => m.Id)], [.. all.Select(m => m.Bounds)]);
    }

    /// <summary>Index of the monitor containing <paramref name="point"/>, or −1.</summary>
    public int IndexAt(PhysicalPoint point) => Array.FindIndex(Bounds, rect => rect.Contains(point));

    /// <summary>One flag per monitor: frozen in <paramref name="state"/>.</summary>
    public bool[] FrozenMask(FreezeState state) => [.. Ids.Select(state.Contains)];
}
