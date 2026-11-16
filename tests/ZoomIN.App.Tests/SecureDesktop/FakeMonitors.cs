using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.SecureDesktop;

/// <summary>Monitors with fixed bounds.</summary>
internal sealed class FakeMonitors(params (string Id, PhysicalRect Bounds)[] monitors) : IMonitorProvider
{
    public IReadOnlyList<string> MonitorIds() => [.. monitors.Select(m => m.Id)];

    public string MonitorAt(PhysicalPoint point) => monitors.FirstOrDefault(m => m.Bounds.Contains(point)).Id ?? monitors[0].Id;

    public PhysicalRect? BoundsOf(string monitorId) => monitors.Where(m => m.Id == monitorId).Select(m => (PhysicalRect?)m.Bounds).FirstOrDefault();
}
