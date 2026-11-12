using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>The edit mode grabs only what the user sees (M19 B1), and a drag can be undone from outside.</summary>
public sealed class IslandEditorGrabTests
{
    [Fact]
    public void PressInsideAnIsland_NextToAHigherOnesEdge_GrabsTheLowerOne()
    {
        // Lower (index 0): picture (100,100)–(200,200). Higher (index 1): picture (205,100)–(305,200).
        var set = One().Add(new Island(0, IslandMonitors.First, new PhysicalRect(400, 400, 50, 50), new PhysicalPoint(205, 100), 2))!;
        var editor = Editor(set);

        Assert.Equal(IslandEditOutcome.Selected, editor.Press(new PhysicalPoint(196, 150)));
        Assert.Equal(0, editor.Selected);
    }

    [Fact]
    public void IslandsThatAreNotDrawn_CannotBeGrabbedOrDeleted()
    {
        var hidden = IslandSet.Empty
            .Add(new Island(0, @"\\.\GONE", new PhysicalRect(0, 0, 50, 50), new PhysicalPoint(100, 100), 2))!
            .Add(new Island(0, IslandMonitors.First, new PhysicalRect(0, 0, 50, 50), new PhysicalPoint(1900, 100), 2))!;
        var editor = Editor(hidden);

        editor.Move(new PhysicalPoint(150, 150));
        Assert.Equal(IslandEditOutcome.None, editor.Delete());
        Assert.Equal(IslandEditOutcome.None, editor.Wheel(1, new PhysicalPoint(1910, 150)));
        Assert.Equal(IslandEditOutcome.Dragged, editor.Press(new PhysicalPoint(150, 150)));
        Assert.Equal(2, editor.Set.Count);
    }

    [Fact]
    public void CancelDrag_UndoesAMove_AndReportsWhetherOneRan()
    {
        var editor = Editor(One());
        Assert.False(editor.CancelDrag());
        editor.Press(new PhysicalPoint(150, 150));
        editor.Move(new PhysicalPoint(300, 300));

        Assert.True(editor.CancelDrag());
        Assert.Equal(new PhysicalPoint(100, 100), editor.Set[0].TargetOrigin);
        Assert.Null(editor.DragBounds);
    }

    [Fact]
    public void Click_ClearsTheRubberBand()
    {
        var editor = Editor();
        editor.Press(new PhysicalPoint(500, 500));
        editor.Move(new PhysicalPoint(505, 505));
        Assert.NotNull(editor.RubberBand);

        Assert.Equal(IslandEditOutcome.Dragged, editor.Release(new PhysicalPoint(505, 505)));
        Assert.Null(editor.RubberBand);
    }

    [Fact]
    public void CornerGrabbedInsideTheHandle_FollowsThePointer_WithoutAJump()
    {
        var editor = Editor(One());

        // 8 px inside the bottom-right corner (200,200); a 1 px move changes the picture by 1 px.
        editor.Press(new PhysicalPoint(192, 192));
        editor.Move(new PhysicalPoint(194, 194));

        Assert.Equal(new PhysicalRect(100, 100, 102, 102), editor.Set[0].Target);
    }

    [Fact]
    public void CornerDraggedPastTheMonitorEdge_StopsThere_AndKeepsTheOppositeCorner()
    {
        // Source (100,0) 100×100 at 4×, picture (500,100)–(900,500).
        var set = IslandSet.Empty.Add(new Island(0, IslandMonitors.First, new PhysicalRect(100, 0, 100, 100), new PhysicalPoint(500, 100), 4))!;
        var editor = Editor(set);

        editor.Press(new PhysicalPoint(500, 100));
        editor.Release(new PhysicalPoint(-300, 100));

        var island = editor.Set[0];
        Assert.Equal(4, island.Level);
        Assert.Equal(900, island.Target.Right);
        Assert.Equal(500, island.Target.Bottom);
        Assert.Equal(200, island.Source.Right);
        Assert.True(island.Source.X >= 0);
    }

    [Fact]
    public void LeftCornerDrag_AtAHighLevel_KeepsTheRightEdgeStill()
    {
        // Source (500,500) 50×50 at 8×, picture (100,100)–(500,500).
        var set = IslandSet.Empty.Add(new Island(0, IslandMonitors.First, new PhysicalRect(500, 500, 50, 50), new PhysicalPoint(100, 100), 8))!;
        var editor = Editor(set);
        editor.Press(new PhysicalPoint(100, 100));

        for (var x = 90; x > 60; x -= 3)
        {
            editor.Move(new PhysicalPoint(x, 100));
            Assert.Equal(500, editor.Set[0].Target.Right);
            Assert.Equal(550, editor.Set[0].Source.Right);
        }
    }

    private static IslandEditor Editor(IslandSet? set = null) => new(set ?? IslandSet.Empty, new IslandMonitors(), _ => 3);

    // Source (200,200) 50×50 at 2×, picture (100,100)–(200,200).
    private static IslandSet One() =>
        IslandSet.Empty.Add(new Island(0, IslandMonitors.First, new PhysicalRect(200, 200, 50, 50), new PhysicalPoint(100, 100), 2))!;
}
