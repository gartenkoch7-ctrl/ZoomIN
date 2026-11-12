using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>The edit mode as a state machine (M19 B1): drag out, move, resize, wheel, Del, Esc.</summary>
public sealed class IslandEditorTests
{
    [Fact]
    public void DragOut_CreatesAnIsland_AtTheMonitorLevel()
    {
        var editor = Editor();

        Assert.Equal(IslandEditOutcome.Dragged, editor.Press(new PhysicalPoint(900, 500)));
        editor.Move(new PhysicalPoint(950, 520));
        Assert.Equal(new PhysicalRect(900, 500, 50, 20), editor.RubberBand);
        Assert.Equal(IslandMonitors.FirstBounds, editor.DragBounds);

        Assert.Equal(IslandEditOutcome.Created, editor.Release(new PhysicalPoint(1000, 540)));

        var island = Assert.Single(editor.Set.Items);
        Assert.Equal(new PhysicalRect(900, 500, 100, 40), island.Source);
        Assert.Equal(3, island.Level);
        Assert.Equal(0, editor.Selected);
        Assert.Null(editor.RubberBand);
        Assert.Null(editor.DragBounds);
    }

    [Fact]
    public void DragOut_UpwardsAndLeft_GivesTheSameRectangle()
    {
        var editor = Editor();

        editor.Press(new PhysicalPoint(1000, 540));
        editor.Release(new PhysicalPoint(900, 500));

        Assert.Equal(new PhysicalRect(900, 500, 100, 40), editor.Set[0].Source);
    }

    [Fact]
    public void DragOut_StopsAtTheMonitorEdge()
    {
        var editor = Editor();

        editor.Press(new PhysicalPoint(1800, 100));
        editor.Release(new PhysicalPoint(2500, 200));

        Assert.Equal(new PhysicalRect(1800, 100, 120, 100), editor.Set[0].Source);
        Assert.Equal(IslandMonitors.First, editor.Set[0].MonitorId);
    }

    [Fact]
    public void ShortDrag_IsAClick_AndCreatesNothing()
    {
        var editor = Editor();

        editor.Press(new PhysicalPoint(900, 500));

        Assert.Equal(IslandEditOutcome.None, editor.Release(new PhysicalPoint(905, 560)));
        Assert.Equal(0, editor.Set.Count);
    }

    [Fact]
    public void FifthIsland_IsRefused()
    {
        var editor = Editor(Four());

        editor.Press(new PhysicalPoint(100, 800));

        Assert.Equal(IslandEditOutcome.Full, editor.Release(new PhysicalPoint(200, 900)));
        Assert.Equal(4, editor.Set.Count);
    }

    [Fact]
    public void Escape_DuringDragOut_Cancels_ThenEnds()
    {
        var editor = Editor();
        editor.Press(new PhysicalPoint(900, 500));
        editor.Move(new PhysicalPoint(1000, 600));

        Assert.Equal(IslandEditOutcome.Cancelled, editor.Escape());
        Assert.Null(editor.RubberBand);
        Assert.Equal(IslandEditOutcome.None, editor.Release(new PhysicalPoint(1000, 600)));
        Assert.Equal(0, editor.Set.Count);
        Assert.Equal(IslandEditOutcome.End, editor.Escape());
    }

    [Fact]
    public void DragIsland_MovesIt_WithinTheMonitor()
    {
        var editor = Editor(One());

        Assert.Equal(IslandEditOutcome.Selected, editor.Press(new PhysicalPoint(150, 150)));
        editor.Move(new PhysicalPoint(250, 180));
        Assert.Equal(new PhysicalPoint(200, 130), editor.Set[0].TargetOrigin);

        Assert.Equal(IslandEditOutcome.Changed, editor.Release(new PhysicalPoint(5000, 5000)));
        Assert.Equal(new PhysicalPoint(1820, 980), editor.Set[0].TargetOrigin);
        Assert.Equal(new PhysicalRect(200, 200, 50, 50), editor.Set[0].Source);
    }

    [Fact]
    public void Escape_DuringMove_PutsTheIslandBack()
    {
        var editor = Editor(One());
        editor.Press(new PhysicalPoint(150, 150));
        editor.Move(new PhysicalPoint(400, 400));

        Assert.Equal(IslandEditOutcome.Cancelled, editor.Escape());
        Assert.Equal(new PhysicalPoint(100, 100), editor.Set[0].TargetOrigin);
    }

    [Fact]
    public void PressAndReleaseInPlace_SelectsWithoutChange()
    {
        var editor = Editor(One());

        editor.Press(new PhysicalPoint(150, 150));

        Assert.Equal(IslandEditOutcome.None, editor.Release(new PhysicalPoint(150, 150)));
        Assert.Equal(0, editor.Selected);
    }

    [Fact]
    public void DragCorner_ShowsMoreOfTheSource_AtTheSameLevel()
    {
        var editor = Editor(One());

        // Bottom-right corner of the picture (100,100)–(200,200) at 2×.
        editor.Press(new PhysicalPoint(199, 199));
        editor.Release(new PhysicalPoint(300, 240));

        var island = editor.Set[0];
        Assert.Equal(2, island.Level);
        Assert.Equal(new PhysicalRect(200, 200, 100, 70), island.Source);
        Assert.Equal(new PhysicalRect(100, 100, 200, 140), island.Target);
    }

    [Fact]
    public void DragTopLeftCorner_KeepsTheOppositeCorner()
    {
        var editor = Editor(One());

        editor.Press(new PhysicalPoint(101, 101));
        editor.Release(new PhysicalPoint(60, 80));

        var island = editor.Set[0];
        Assert.Equal(new PhysicalRect(60, 80, 140, 120), island.Target);
        Assert.Equal(250, island.Source.Right);
        Assert.Equal(250, island.Source.Bottom);
    }

    [Fact]
    public void DragCorner_PastTheOppositeOne_StopsAtTheMinimum()
    {
        var editor = Editor(One());

        editor.Press(new PhysicalPoint(199, 199));
        editor.Release(new PhysicalPoint(0, 0));

        Assert.Equal(IslandRules.MinSize, editor.Set[0].Source.Width);
        Assert.Equal(IslandRules.MinSize, editor.Set[0].Source.Height);
    }

    [Fact]
    public void Wheel_OverAnIsland_ChangesItsLevel_WithinTheLimits()
    {
        var editor = Editor(One());

        Assert.Equal(IslandEditOutcome.LevelChanged, editor.Wheel(1, new PhysicalPoint(150, 150)));
        Assert.Equal(2.5, editor.Set[0].Level, 6);
        Assert.Equal(0, editor.Selected);

        editor.Wheel(-100, new PhysicalPoint(1000, 1000)); // beside it: the selected one
        Assert.Equal(1, editor.Set[0].Level);
    }

    [Fact]
    public void Wheel_WithNothingThere_DoesNothing()
    {
        var editor = Editor();

        Assert.Equal(IslandEditOutcome.None, editor.Wheel(1, new PhysicalPoint(500, 500)));
    }

    [Fact]
    public void Delete_RemovesTheSelected_OrTheOneUnderThePointer()
    {
        var editor = Editor(Four());
        editor.Press(new PhysicalPoint(450, 150));
        editor.Release(new PhysicalPoint(450, 150));

        Assert.Equal(IslandEditOutcome.Deleted, editor.Delete());
        Assert.Equal(3, editor.Set.Count);
        Assert.Equal(-1, editor.Selected);

        editor.Move(new PhysicalPoint(150, 150));
        Assert.Equal(IslandEditOutcome.Deleted, editor.Delete());
        Assert.Equal(2, editor.Set.Count);

        editor.Move(new PhysicalPoint(1500, 900));
        Assert.Equal(IslandEditOutcome.None, editor.Delete());
    }

    [Fact]
    public void NewPress_DuringADrag_CancelsItFirst()
    {
        var editor = Editor(One());
        editor.Press(new PhysicalPoint(150, 150));
        editor.Move(new PhysicalPoint(600, 600));

        editor.Press(new PhysicalPoint(1000, 900));

        Assert.Equal(new PhysicalPoint(100, 100), editor.Set[0].TargetOrigin);
        Assert.NotNull(editor.RubberBand);
    }

    [Fact]
    public void PressBesideAnIsland_NearItsEdge_StartsARubberBand()
    {
        var editor = Editor(One());

        Assert.Equal(IslandEditOutcome.Dragged, editor.Press(new PhysicalPoint(150, 95)));
        Assert.Equal(-1, editor.Selected);
    }

    private static IslandEditor Editor(IslandSet? set = null) => new(set ?? IslandSet.Empty, new IslandMonitors(), _ => 3);

    // Source (200,200) 50×50 at 2×, picture (100,100)–(200,200).
    private static IslandSet One() =>
        IslandSet.Empty.Add(new Island(0, IslandMonitors.First, new PhysicalRect(200, 200, 50, 50), new PhysicalPoint(100, 100), 2))!;

    // Pictures 100×100 at x = 100, 400, 700, 1000.
    private static IslandSet Four()
    {
        var set = IslandSet.Empty;
        for (var i = 0; i < 4; i++)
        {
            set = set.Add(new Island(0, IslandMonitors.First, new PhysicalRect(i * 60, 0, 50, 50), new PhysicalPoint(100 + (i * 300), 100), 2))!;
        }

        return set;
    }
}
