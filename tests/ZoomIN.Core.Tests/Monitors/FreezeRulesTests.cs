using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Monitors;

/// <summary>Which freezes survive a display change (G2): never a trapped or invisible pointer.</summary>
public sealed class FreezeRulesTests
{
    private static readonly PhysicalRect s_left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect s_right = new(1920, 0, 1920, 1080);

    [Fact]
    public void AnUnchangedLiveFreeze_StaysWhileAFreeMonitorIsLeft()
    {
        var right = Frozen("R", s_right, still: false);
        var state = State(right);

        Assert.Equal([right], FreezeRules.KeptAfterDisplayChange(state, [("L", s_left), ("R", s_right)], new PhysicalPoint(10, 10)));
        Assert.Equal([right], FreezeRules.KeptAfterDisplayChange(state, [("L", s_left), ("R", s_right)], null));
    }

    [Fact]
    public void GoneChangedOrStill_Ends()
    {
        Assert.Empty(FreezeRules.KeptAfterDisplayChange(State(Frozen("R", s_right, false)), [("L", s_left)], null));
        Assert.Empty(FreezeRules.KeptAfterDisplayChange(
            State(Frozen("R", s_right, false)), [("L", s_left), ("R", s_right with { Width = 2560 })], null));
        Assert.Empty(FreezeRules.KeptAfterDisplayChange(State(Frozen("R", s_right, true)), [("L", s_left), ("R", s_right)], null));
    }

    [Fact]
    public void NoFreeMonitor_OrThePointerOnAFrozenOne_EndsAll()
    {
        var right = Frozen("R", s_right, false);

        Assert.Empty(FreezeRules.KeptAfterDisplayChange(State(right), [("R", s_right)], null));
        Assert.Empty(FreezeRules.KeptAfterDisplayChange(State(right), [("L", s_left), ("R", s_right)], new PhysicalPoint(2000, 10)));
    }

    [Fact]
    public void DisplayChange_WithThePointerOnAFreezeMadeAfterTheEvent_EndsItToo()
    {
        var right = Frozen("R", s_right, false);
        var now = State(right);

        var lift = FreezeRules.ToLift(
            ZoomIN.Core.PointerFence.FenceTrigger.DisplayChange, now, FreezeState.Empty, false, [("L", s_left), ("R", s_right)], new PhysicalPoint(2000, 5));

        Assert.Equal([right], lift);
    }

    [Fact]
    public void DefaultAllMonitors_PairsIdsWithBounds_OrGivesNothing()
    {
        IMonitorProvider monitors = new Fixed(("L", s_left), ("R", s_right));
        Assert.Equal([("L", s_left), ("R", s_right)], monitors.AllMonitors());

        IMonitorProvider broken = new Fixed(("L", s_left), ("X", null));
        Assert.Empty(broken.AllMonitors());
    }

    private static FreezeState State(FrozenMonitor monitor)
    {
        var freeze = new MonitorFreeze();
        freeze.Freeze(monitor);
        return freeze.State;
    }

    private static FrozenMonitor Frozen(string id, PhysicalRect bounds, bool still) =>
        new(id, bounds, 2, ZoomMode.Fullscreen, LensSize.Default, DockLayout.Default, still, RgbColor.Cyan);

    private sealed class Fixed(params (string Id, PhysicalRect? Bounds)[] monitors) : IMonitorProvider
    {
        public IReadOnlyList<string> MonitorIds() => [.. monitors.Select(m => m.Id)];

        public string MonitorAt(PhysicalPoint point) => monitors[0].Id;

        public PhysicalRect? BoundsOf(string monitorId) => monitors.First(m => m.Id == monitorId).Bounds;
    }
}
