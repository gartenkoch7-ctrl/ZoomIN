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
    public void ZoomCommands_IgnoreAFrozenMonitorsLevel()
    {
        string[] all = ["L", "R"];
        var model = new ZoomIN.Core.Zoom.ZoomModel(new ZoomIN.Core.Zoom.ZoomLevels(), ZoomMode.Fullscreen, true, 0);
        model.Step(4, "R", all); // only R is zoomed (per monitor)
        model.Frozen.Freeze(Frozen("R", s_right, false));
        Assert.False(model.IsOn);

        // Ctrl+Alt+Z on the free monitor switches zoom on there at the first press, R's level stays.
        model.Toggle("L", all);
        Assert.True(model.TargetFor("L") > 1);
        Assert.True(model.TargetFor("R") > 1);
        model.Step(-20, "L", all);
        Assert.False(model.IsOn);
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
