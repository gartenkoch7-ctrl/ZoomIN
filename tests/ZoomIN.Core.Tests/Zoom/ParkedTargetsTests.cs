using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Zoom;

/// <summary>Zoom commands while a monitor is frozen (G2): its target is parked and comes back on thaw if it still fits.</summary>
public sealed class ParkedTargetsTests
{
    private static readonly string[] s_all = ["L", "R"];

    [Fact]
    public void ZoomOnOff_SeesOnlyTheFreeMonitors_AndTheFrozenLevelComesBack()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, true, 0);
        model.Step(4, "R", s_all); // only R is zoomed (per monitor)
        var level = model.TargetFor("R");
        var changes = 0;
        model.Changed += () => changes++;

        model.Frozen.Freeze(Frozen("R", level));
        Assert.Equal(1, changes);
        Assert.False(model.IsOn);
        Assert.True(ZoomMapping.SavedOn(model)); // the secure desktop and the next start still zoom

        // Ctrl+Alt+Z on the free monitor switches zoom on there at the first press, then off again.
        model.Toggle("L", s_all);
        Assert.True(model.TargetFor("L") > 1);
        model.Toggle("L", s_all);
        Assert.False(model.IsOn);

        model.Frozen.UnfreezeAll();
        Assert.Equal(level, model.TargetFor("R"));
        Assert.True(model.IsOn);
    }

    [Fact]
    public void AModeChangeWhileFrozen_ThawsAt1x()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, true, 0);
        model.Step(4, "R", s_all);
        model.Frozen.Freeze(Frozen("R", model.TargetFor("R")));

        model.SetMode(ZoomMode.Lens); // e.g. a profile switch
        model.Frozen.UnfreezeAll();

        Assert.Equal(1, model.TargetFor("R"));
    }

    [Fact]
    public void InDockedMode_OnlyTheDockedMonitorGetsItsTargetBack()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Docked, true, 0);
        model.Activate(ZoomMode.Docked, "R", s_all); // the strip on R
        Assert.True(model.TargetFor("R") > 1);

        model.Frozen.Freeze(Frozen("L", 1, ZoomMode.Docked));
        model.Frozen.Freeze(Frozen("R", model.TargetFor("R"), ZoomMode.Docked));
        model.Frozen.UnfreezeAll();

        Assert.True(model.TargetFor("R") > 1);
        Assert.Equal(1, model.TargetFor("L"));
    }

    [Fact]
    public void SharedLevel_ThawsAtTheLevelTheOthersHaveNow_AndADisplayChangeThawsAt1x()
    {
        string[] three = ["A", "B", "C"];
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, false, 0);
        model.Toggle("A", three);
        model.Frozen.Freeze(Frozen("A", model.TargetFor("A")));
        model.Step(3, "B", three);
        var now = model.TargetFor("B");

        model.Frozen.UnfreezeAll();
        Assert.Equal(now, model.TargetFor("A"));

        model.Frozen.Freeze(Frozen("A", now));
        model.Frozen.Unfreeze(_ => true, restoreLevel: false);
        Assert.Equal(1, model.TargetFor("A"));
    }

    [Fact]
    public void AThawedMonitor_HasItsTarget_BeforeTheThawIsVisible()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, true, 0);
        model.Step(4, "R", s_all);
        var level = model.TargetFor("R");
        model.Frozen.Freeze(Frozen("R", level));
        var seen = 0.0;
        model.Frozen.Changed += () => seen = model.Frozen.IsFrozen("R") ? -1 : model.TargetFor("R");

        model.Frozen.UnfreezeAll();

        Assert.Equal(level, seen);
    }

    [Fact]
    public void AWindowDragLevel_SetWhileFrozen_HoldsTheMonitorAfterTheThawUntilReleased()
    {
        // M19 integration: a window drag (M17 G1) may set its level on a frozen monitor (MonitorRenderer draws
        // the frozen level meanwhile, frozen?.Level first); after the thaw the drag still holds it.
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, true, 0);
        model.Step(4, "R", s_all);
        var level = model.TargetFor("R");
        model.Frozen.Freeze(Frozen("R", level));

        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, "R", ZoomLimits.Min);

        model.Frozen.UnfreezeAll();
        Assert.Equal(ZoomLimits.Min, model.EffectiveTargetFor("R")); // thawed during the drag: the drag's level
        model.Temporary.Release(TemporaryZoomOwner.WindowDrag, "R");
        Assert.Equal(level, model.EffectiveTargetFor("R"));
    }

    private static FrozenMonitor Frozen(string id, double level, ZoomMode mode = ZoomMode.Fullscreen) =>
        new(id, new PhysicalRect(0, 0, 10, 10), level, mode, LensSize.Default, DockLayout.Default, false, RgbColor.Cyan);
}
