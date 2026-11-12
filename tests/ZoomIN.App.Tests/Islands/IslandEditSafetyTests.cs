using ZoomIN.Core.Islands;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.Islands;

/// <summary>The edit mode never overwrites changes from outside, never saves half a drag and never starts without its hook (M19 B1).</summary>
public sealed class IslandEditSafetyTests
{
    private static readonly IslandEntry s_profileIsland = new()
    {
        MonitorId = Overview.TwoMonitors.First, SourceX = 10, SourceY = 10, SourceWidth = 40, SourceHeight = 20,
        TargetX = 500, TargetY = 500, Level = 2,
    };

    [Fact]
    public void WithoutTheHook_TheEditModeDoesNotStart()
    {
        using var rig = new IslandRig();
        rig.Input.IsAvailable = false;

        rig.Controller.ToggleEdit();

        Assert.False(rig.Controller.IsEditing);
        Assert.Equal(0, rig.Input.Starts);
        Assert.Null(rig.Level(Overview.TwoMonitors.First));
        Assert.Equal(["Islands cannot be edited right now: ZoomIN cannot take the mouse clicks"], rig.Announcer.Messages);
    }

    [Fact]
    public void ProfileSwitch_DuringEditing_EndsIt_AndKeepsTheProfilesIslands()
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();
        rig.Handler.Pressed(new PhysicalPoint(100, 100));
        rig.Handler.Released(new PhysicalPoint(140, 120));

        rig.Settings.ApplyDisplay(new AppSettings { Islands = new IslandOptions { Items = [s_profileIsland] } });

        Assert.False(rig.Controller.IsEditing);
        Assert.Equal(new PhysicalRect(10, 10, 40, 20), Assert.Single(rig.Islands.Scene.Set.Items).Source);
        Assert.Equal(10, Assert.Single(rig.Settings.Current.Islands.Items).SourceX);
    }

    [Fact]
    public void RemoveOnTheSettingsPage_DuringEditing_IsNotUndone()
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();
        rig.Handler.Pressed(new PhysicalPoint(100, 100));
        rig.Handler.Released(new PhysicalPoint(140, 120));
        var handler = rig.Handler;

        rig.Islands.Remove(rig.Islands.Scene.Set[0].Id);
        handler.Moved(new PhysicalPoint(300, 300));

        Assert.False(rig.Controller.IsEditing);
        Assert.Equal(0, rig.Islands.Scene.Set.Count);
    }

    [Fact]
    public void EndInTheMiddleOfADrag_PutsTheIslandBack()
    {
        using var rig = new IslandRig();
        rig.Controller.ToggleEdit();
        rig.Handler.Pressed(new PhysicalPoint(100, 100));
        rig.Handler.Released(new PhysicalPoint(140, 120));
        var placed = rig.Islands.Scene.Set[0].TargetOrigin;
        var picture = rig.Islands.Scene.Set[0].Target;
        var inside = new PhysicalPoint(picture.X + (picture.Width / 2), picture.Y + (picture.Height / 2));
        rig.Handler.Pressed(inside);
        rig.Handler.Moved(new PhysicalPoint(inside.X + 300, inside.Y + 200));

        rig.Fence.Raise(Core.PointerFence.FenceTrigger.DisplayChange);

        Assert.Equal(placed, rig.Islands.Scene.Set[0].TargetOrigin);
    }

    [Fact]
    public void EditMode_LeavesHiddenIslandsHidden()
    {
        using var rig = new IslandRig(new AppSettings { Islands = new IslandOptions { Visible = false } });

        rig.Controller.ToggleEdit();
        rig.Handler.Key(IslandEditKey.Escape);

        Assert.False(rig.Islands.Scene.Visible);
    }
}
