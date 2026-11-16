using ZoomIN.Core.Islands;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>Islands that freeze with a monitor (B1 × G2): kept without edit view, counted by the drawing rules.</summary>
public sealed class FrozenIslandsTests
{
    private const string First = @"\\.\DISPLAY1";
    private const string Second = @"\\.\DISPLAY2";
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void ShownIslands_CountsOnlyItsOwn_ThatFit_WhileVisible_InFullscreenAboveOneTimes()
    {
        var set = IslandSet.Empty
            .Add(new Island(0, First, new PhysicalRect(0, 0, 50, 50), new PhysicalPoint(100, 100), 2))!
            .Add(new Island(0, First, new PhysicalRect(100, 0, 50, 50), new PhysicalPoint(1900, 1000), 2))! // does not fit
            .Add(new Island(0, Second, new PhysicalRect(0, 0, 50, 50), new PhysicalPoint(100, 100), 2))!;
        var scene = new IslandScene(set, true, null);

        Assert.Equal(1, Frozen(scene, ZoomMode.Fullscreen, 3).ShownIslands);
        Assert.Equal(0, Frozen(scene, ZoomMode.Lens, 3).ShownIslands);
        Assert.Equal(0, Frozen(scene, ZoomMode.Fullscreen, 1).ShownIslands);
        Assert.Equal(0, Frozen(scene with { Visible = false }, ZoomMode.Fullscreen, 3).ShownIslands);
        Assert.Equal(0, Frozen(null, ZoomMode.Fullscreen, 3).ShownIslands);
    }

    [Fact]
    public void Islands_AreKeptWithoutTheEditView()
    {
        var scene = new IslandScene(IslandSet.Empty, true, new IslandEditView(-1, null, 0));

        var frozen = Frozen(scene, ZoomMode.Fullscreen, 2);

        Assert.Null(frozen.Islands!.Edit);
        Assert.Same(scene.Set, frozen.Islands.Set);
    }

    private static FrozenMonitor Frozen(IslandScene? islands, ZoomMode mode, double level) =>
        new(First, s_monitor, level, mode, LensSize.Default, DockLayout.Default, false, default, null, islands);
}
