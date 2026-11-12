using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Islands;
using ZoomIN.App.Localization;
using ZoomIN.App.Tests.Freeze;
using ZoomIN.App.Tests.Overview;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;
using static ZoomIN.App.Tests.Freeze.ThreeMonitors;

namespace ZoomIN.App.Tests.Islands;

/// <summary>Island edit mode (B1) next to a frozen monitor (G2): the freeze stays, with its level and fence.</summary>
public sealed class IslandFreezeTests
{
    [Fact]
    public void Edit_KeepsAFrozenMonitorFrozen_AtItsLevel_AndFenced()
    {
        using var rig = new FreezeRig();
        rig.Models.Zoom.Activate(ZoomMode.Fullscreen, Two, [One, Two, Three]);
        rig.PointerTo(Two);
        rig.Controller.ToggleFreeze();
        var level = rig.Frozen.For(Two)!.Level;
        using var islands = Islands(rig);

        islands.ToggleEdit();

        Assert.True(islands.IsEditing);
        Assert.Equal(level, rig.Frozen.For(Two)!.Level);
        Assert.Equal([new PhysicalRect(3840, 0, 1920, 1080)], rig.Fence.Forbidden);

        // Unfrozen while editing: the monitor joins the edit mode at 1×.
        rig.Controller.ToggleFreeze();
        Assert.False(rig.Frozen.Any);
        Assert.Equal(ZoomLimits.Min, rig.Models.Zoom.EffectiveTargetFor(Two));

        islands.End("test");
        Assert.Equal(level, rig.Models.Zoom.EffectiveTargetFor(Two));
    }

    [Fact]
    public void Edit_EndedWhileFrozen_LeavesTheFreezeAndItsFence()
    {
        using var rig = new FreezeRig();
        rig.Models.Zoom.Activate(ZoomMode.Fullscreen, Two, [One, Two, Three]);
        rig.PointerTo(Two);
        rig.Controller.ToggleFreeze();
        using var islands = Islands(rig);

        islands.ToggleEdit();
        islands.ToggleEdit();

        Assert.False(islands.IsEditing);
        Assert.True(rig.Frozen.IsFrozen(Two));
        Assert.Equal([new PhysicalRect(3840, 0, 1920, 1080)], rig.Fence.Forbidden);
    }

    [Fact]
    public void Freeze_WhileEditing_IsRefused_WithAHint()
    {
        using var rig = new FreezeRig();
        rig.PointerTo(Two);
        using var islands = Islands(rig);
        islands.ToggleEdit();

        rig.Controller.ToggleFreeze();

        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Mover.Moves);
        Assert.Empty(rig.Fence.Forbidden);
        Assert.Equal(["Finish editing the islands first, then freeze."], rig.Hints);
    }

    private static IslandController Islands(FreezeRig rig)
    {
        var texts = AppTexts.CreateLocalizer();
        texts.SetLanguage(Core.Settings.AppLanguage.English);
        return new IslandController(rig.Settings, rig.Monitors, new FakeIslandInput(), rig.Fence, new RecordingAnnouncer(), texts,
            new TimerClock(), NullLogger<IslandController>.Instance);
    }
}
