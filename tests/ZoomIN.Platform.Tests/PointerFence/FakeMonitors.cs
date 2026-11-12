using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Tests.PointerFence;

/// <summary>Monitors 1 · 3 · 2 of the M19 demo; can be changed like a display change.</summary>
internal sealed class FakeMonitors : IMonitorProvider
{
    public static readonly PhysicalRect Left = new(0, 0, 1920, 1080);
    public static readonly PhysicalRect Middle = new(1920, 0, 1920, 1080);
    public static readonly PhysicalRect Right = new(3840, 0, 1920, 1080);

    public Dictionary<string, PhysicalRect> Bounds { get; } = new()
    {
        [@"\\.\DISPLAY1"] = Left,
        [@"\\.\DISPLAY3"] = Middle,
        [@"\\.\DISPLAY2"] = Right,
    };

    public bool Throws { get; set; }

    public IReadOnlyList<string> MonitorIds() => Throws ? throw new InvalidOperationException("monitors") : [.. Bounds.Keys];

    public string MonitorAt(PhysicalPoint point) => Bounds.FirstOrDefault(pair => pair.Value.Contains(point)).Key ?? @"\\.\DISPLAY1";

    public PhysicalRect? BoundsOf(string monitorId) => Bounds.TryGetValue(monitorId, out var bounds) ? bounds : null;
}
