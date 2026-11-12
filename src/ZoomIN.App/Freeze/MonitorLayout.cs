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

    /// <summary>Monitors whose bounds Windows reports now; one that cannot be described is left out.</summary>
    public static MonitorLayout Read(IMonitorProvider monitors)
    {
        var ids = new List<string>();
        var bounds = new List<PhysicalRect>();
        foreach (var id in monitors.MonitorIds())
        {
            if (monitors.BoundsOf(id) is { Width: > 0, Height: > 0 } rect)
            {
                ids.Add(id);
                bounds.Add(rect);
            }
        }

        return new MonitorLayout([.. ids], [.. bounds]);
    }

    /// <summary>Index of the monitor containing <paramref name="point"/>, or −1.</summary>
    public int IndexAt(PhysicalPoint point) => Array.FindIndex(Bounds, rect => rect.Contains(point));

    /// <summary>Index of the monitor, or −1 if it is gone.</summary>
    public int IndexOf(string monitorId) => Array.IndexOf(Ids, monitorId);

    /// <summary>One flag per monitor: frozen in <paramref name="state"/>.</summary>
    public bool[] FrozenMask(FreezeState state) => [.. Ids.Select(state.Contains)];
}
