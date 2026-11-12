using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>Two monitors side by side, 1920×1080 each, for the island tests.</summary>
internal sealed class IslandMonitors : IMonitorProvider
{
    public const string First = @"\\.\DISPLAY1";
    public const string Second = @"\\.\DISPLAY2";

    public static readonly PhysicalRect FirstBounds = new(0, 0, 1920, 1080);
    public static readonly PhysicalRect SecondBounds = new(1920, 0, 1920, 1080);

    public IReadOnlyList<string> MonitorIds() => [First, Second];

    public string MonitorAt(PhysicalPoint point) => point.X >= 1920 ? Second : First;

    public PhysicalRect? BoundsOf(string monitorId) => monitorId switch
    {
        First => FirstBounds,
        Second => SecondBounds,
        _ => null,
    };
}
