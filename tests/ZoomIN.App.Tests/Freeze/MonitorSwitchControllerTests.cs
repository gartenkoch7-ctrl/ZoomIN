using ZoomIN.Core.Viewport;
using static ZoomIN.App.Tests.Freeze.ThreeMonitors;

namespace ZoomIN.App.Tests.Freeze;

/// <summary>Ctrl+Alt+N (G2) in the grilling layout 1 · 3 · 2: layout order and the last position per monitor.</summary>
public sealed class MonitorSwitchControllerTests
{
    [Fact]
    public void WithoutFreeze_TheSwitchFollowsTheLayout_AndReturnsToTheLastPosition()
    {
        using var rig = new FreezeRig();
        rig.Cursor.Position = new PhysicalPoint(100, 900);

        rig.Switch.NextMonitor();
        Assert.Equal(MiddleOf(Three), rig.Cursor.Position);
        rig.Switch.NextMonitor();
        Assert.Equal(Two, rig.PointerMonitor);
        rig.Switch.NextMonitor();
        Assert.Equal(Three, rig.PointerMonitor);
        rig.Switch.NextMonitor();

        // Back on 1 at its last position, not in its middle.
        Assert.Equal(new PhysicalPoint(100, 900), rig.Cursor.Position);
        Assert.Contains("Monitor 3", rig.Announcer.Messages);
    }

    [Fact]
    public void FreezeJump_GoesToTheLastPositionOnTheTargetMonitor()
    {
        using var rig = new FreezeRig();
        rig.Cursor.Position = new PhysicalPoint(2500, 100);
        rig.Switch.NextMonitor(); // 3 → 2, remembers (2500, 100) on 3
        rig.Cursor.Position = new PhysicalPoint(5000, 500);

        rig.Controller.ToggleFreeze();

        Assert.Equal(new PhysicalPoint(2500, 100), rig.Cursor.Position);
    }
}
