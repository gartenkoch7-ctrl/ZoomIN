using ZoomIN.App.Hotkeys;
using ZoomIN.App.Tests.Overview;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Modes;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.Islands;

/// <summary>Islands on/off and the edit mode with its safety ends (M19 B1).</summary>
public sealed class IslandControllerTests
{
    [Fact]
    public void Toggle_HidesAndShows_AndAnnounces()
    {
        using var rig = new IslandRig();

        rig.Visibility.Toggle();
        Assert.False(rig.Islands.Scene.Visible);
        rig.Visibility.Toggle();

        Assert.True(rig.Islands.Scene.Visible);
        Assert.Equal(["Islands off", "Islands on"], rig.Announcer.Messages);
    }

    [Fact]
    public void Edit_ShowsOneTimes_TakesTheInput_AndAddsTheDragFence()
    {
        using var rig = new IslandRig();

        rig.Controller.ToggleEdit();

        Assert.True(rig.Controller.IsEditing);
        Assert.Equal(1, rig.Level(TwoMonitors.First));
        Assert.Equal(1, rig.Level(TwoMonitors.Second));
        Assert.True(rig.Input.Capturing);
        Assert.Equal(PointerFilterOrder.IslandEdit, Assert.Single(rig.Fence.Filters).Order);
        Assert.True(rig.Islands.Scene.Editing);
        Assert.Contains("Edit islands: drag a rectangle, Esc ends", rig.Announcer.Messages);
    }

    [Fact]
    public void DragOut_CreatesAnIsland_ConfinesTheDrag_AndAnnounces()
    {
        using var rig = new IslandRig(new AppSettings { LastZoomLevel = 3 });
        rig.Controller.ToggleEdit();

        rig.Handler.Pressed(new PhysicalPoint(1800, 1000));
        rig.Handler.Moved(new PhysicalPoint(1900, 1040));
        Assert.Equal(new PhysicalRect(1800, 1000, 100, 40), rig.Islands.Scene.Edit!.RubberBand);
        var confined = rig.Fence.Filters[0].Filter(0, default, new PhysicalPoint(2500, 500));
        Assert.Equal(new PhysicalPoint(1919, 500), confined);

        rig.Handler.Released(new PhysicalPoint(1900, 1040));

        var island = Assert.Single(rig.Islands.Scene.Set.Items);
        Assert.Equal(3, island.Level);
        Assert.Equal(new PhysicalRect(1620, 960, 300, 120), island.Target);
        Assert.Equal(new PhysicalPoint(2500, 500), rig.Fence.Filters[0].Filter(0, default, new PhysicalPoint(2500, 500)));
        Assert.Contains("Island 1 of 1, 3×", rig.Announcer.Messages);
    }

    [Fact]
    public void Escape_EndsTheEditMode_AndReleasesEverything()
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();
        var handler = rig.Handler;

        handler.Key(IslandEditKey.Escape);

        Assert.False(rig.Controller.IsEditing);
        Assert.Null(rig.Level(TwoMonitors.First));
        Assert.False(rig.Input.Capturing);
        Assert.Empty(rig.Fence.Filters);
        Assert.False(rig.Islands.Scene.Editing);
        Assert.Contains("Edit islands off", rig.Announcer.Messages);

        // Input queued before the end changes nothing.
        handler.Pressed(new PhysicalPoint(10, 10));
        handler.Released(new PhysicalPoint(200, 200));
        Assert.Equal(0, rig.Islands.Scene.Set.Count);
    }

    [Fact]
    public void ShortcutAgain_EndsIt()
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();

        rig.Controller.ToggleEdit();

        Assert.False(rig.Controller.IsEditing);
    }

    [Fact]
    public void Docked_IsRefused()
    {
        using var rig = new IslandRig(new AppSettings { LastMode = ZoomMode.Docked });

        rig.Controller.ToggleEdit();

        Assert.False(rig.Controller.IsEditing);
        Assert.False(rig.Input.Capturing);
        Assert.Equal(["Islands cannot be edited in docked mode"], rig.Announcer.Messages);
    }

    [Theory]
    [InlineData(FenceTrigger.DesktopSwitch)]
    [InlineData(FenceTrigger.SessionUnlock)]
    [InlineData(FenceTrigger.DisplayChange)]
    public void DesktopOrDisplayChange_EndsIt(FenceTrigger trigger)
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();

        rig.Fence.Raise(trigger);

        Assert.False(rig.Controller.IsEditing);
        Assert.Null(rig.Level(TwoMonitors.Second));
    }

    [Fact]
    public void ZoomCommand_ThatClearsTheOneTimes_EndsIt()
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();

        rig.Models.Zoom.SetMode(ZoomMode.Lens);

        Assert.False(rig.Controller.IsEditing);
        Assert.False(rig.Input.Capturing);
    }

    [Fact]
    public void TwoMinutesWithoutInput_EndIt_AndInputWaitsAgain()
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();
        var idle = Assert.Single(rig.Clock.Timers);
        Assert.Equal(TimeSpan.FromMinutes(2), idle.Due);

        rig.Handler.Moved(new PhysicalPoint(5, 5));
        Assert.Equal(1, idle.Changes);

        idle.Fire();

        Assert.False(rig.Controller.IsEditing);
        Assert.True(idle.Disposed);
    }

    [Fact]
    public void FifthIsland_IsRefused_WithAHint()
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();
        for (var i = 0; i < 5; i++)
        {
            rig.Handler.Pressed(new PhysicalPoint(100 + (i * 200), 100));
            rig.Handler.Released(new PhysicalPoint(140 + (i * 200), 120));
        }

        Assert.Equal(IslandRules.MaxCount, rig.Islands.Scene.Set.Count);
        Assert.Contains("At most 4 islands. Delete one first.", rig.Announcer.Messages);
    }

    [Fact]
    public void Delete_RemovesTheSelected_AndIsSaved()
    {
        using var rig = new IslandRig();
        var saved = 0;
        rig.Islands.Saved += () => saved++;
        rig.Controller.ToggleEdit();
        rig.Handler.Pressed(new PhysicalPoint(100, 100));
        rig.Handler.Released(new PhysicalPoint(140, 120));

        rig.Handler.Key(IslandEditKey.Delete);

        Assert.Equal(0, rig.Islands.Scene.Set.Count);
        Assert.Equal(2, saved);
        Assert.Contains("Island deleted", rig.Announcer.Messages);
    }

    [Fact]
    public void Settings_KeepTheIslands_ForTheProfile()
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();
        rig.Handler.Pressed(new PhysicalPoint(100, 100));
        rig.Handler.Released(new PhysicalPoint(140, 120));
        rig.Handler.Key(IslandEditKey.Escape);

        var saved = rig.Settings.Current.Islands;

        var entry = Assert.Single(saved.Items);
        Assert.Equal(TwoMonitors.First, entry.MonitorId);
        Assert.Equal((100, 100, 40, 20), (entry.SourceX, entry.SourceY, entry.SourceWidth, entry.SourceHeight));
    }

    [Fact]
    public void Shortcuts_AreEditable_AndOptional()
    {
        var islands = Assert.Single(HotkeyBinding.All, binding => binding.TextKey == "Act.Islands");
        var edit = Assert.Single(HotkeyBinding.All, binding => binding.TextKey == "Act.IslandEdit");

        Assert.True(islands.Optional);
        Assert.True(edit.Optional);
        Assert.Equal(default, edit.Set(new AppSettings(), default).IslandEditHotkey);
    }
}
