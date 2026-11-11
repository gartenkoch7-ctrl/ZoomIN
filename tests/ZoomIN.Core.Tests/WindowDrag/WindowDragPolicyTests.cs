using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.Core.Tests.WindowDrag;

/// <summary>Which level a window drag holds, and how move and resize are told apart (G1).</summary>
public sealed class WindowDragPolicyTests
{
    [Theory]
    [InlineData(WindowDragAction.Lift, ZoomMode.Fullscreen, 1.0)]
    [InlineData(WindowDragAction.Lower, ZoomMode.Fullscreen, 2.0)]
    [InlineData(WindowDragAction.Lift, ZoomMode.Lens, 1.0)]
    [InlineData(WindowDragAction.Lower, ZoomMode.Lens, 1.0)] // the lens hides while dragging
    public void LevelFor_GivesTheCeilingOfTheAction(WindowDragAction action, ZoomMode mode, double expected)
    {
        var options = WindowDragOptions.Default with { Action = action };

        Assert.Equal(expected, WindowDragPolicy.LevelFor(WindowDragKind.Move, mode, options));
    }

    [Theory]
    [InlineData(WindowDragAction.Lift)]
    [InlineData(WindowDragAction.Lower)]
    [InlineData(WindowDragAction.Unchanged)]
    public void LevelFor_Docked_LeavesTheZoomAlone(WindowDragAction action)
    {
        var options = WindowDragOptions.Default with { Action = action };

        Assert.Null(WindowDragPolicy.LevelFor(WindowDragKind.Move, ZoomMode.Docked, options));
    }

    [Fact]
    public void LevelFor_Unchanged_LeavesTheZoomAlone()
    {
        var options = WindowDragOptions.Default with { Action = WindowDragAction.Unchanged };

        Assert.Null(WindowDragPolicy.LevelFor(WindowDragKind.Resize, ZoomMode.Fullscreen, options));
    }

    [Fact]
    public void LevelFor_Lower_UsesTheChosenLevel()
    {
        var options = WindowDragOptions.Default with { Action = WindowDragAction.Lower, LowerLevel = 3.5 };

        Assert.Equal(3.5, WindowDragPolicy.LevelFor(WindowDragKind.Move, ZoomMode.Fullscreen, options));
    }

    [Theory]
    [InlineData(WindowDragKind.Move, true, false, true)]
    [InlineData(WindowDragKind.Move, false, true, false)]
    [InlineData(WindowDragKind.Resize, true, false, false)]
    [InlineData(WindowDragKind.Resize, false, true, true)]
    [InlineData(WindowDragKind.Unknown, true, true, true)]
    [InlineData(WindowDragKind.Unknown, true, false, false)]
    [InlineData(WindowDragKind.Unknown, false, true, false)]
    public void Triggers_FollowTheTwoSwitches(WindowDragKind kind, bool onMove, bool onResize, bool expected)
    {
        var options = WindowDragOptions.Default with { OnMove = onMove, OnResize = onResize };

        Assert.Equal(expected, WindowDragPolicy.Triggers(kind, options));
        Assert.Equal(expected, WindowDragPolicy.LevelFor(kind, ZoomMode.Fullscreen, options) is not null);
    }

    [Fact]
    public void NullOptions_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => WindowDragPolicy.LevelFor(WindowDragKind.Move, ZoomMode.Fullscreen, null!));
        Assert.Throws<ArgumentNullException>(() => WindowDragPolicy.Triggers(WindowDragKind.Move, null!));
    }

    [Fact]
    public void FromFirstChange_SnappedWindowGettingItsNormalSize_IsAMove()
    {
        var snapped = new PhysicalRect(0, 0, 960, 1040); // left half
        var restored = new PhysicalRect(0, 0, 800, 600); // under the pointer, top-left kept by chance
        var normal = new PhysicalRect(300, 200, 800, 600);

        Assert.Equal(WindowDragKind.Move, WindowDragKinds.FromFirstChange(snapped, restored, normal));
        Assert.Equal(WindowDragKind.Resize, WindowDragKinds.FromFirstChange(snapped, restored)); // without it: an edge kept
        Assert.Equal(WindowDragKind.Resize, WindowDragKinds.FromFirstChange(normal, normal with { Width = 820 }, normal));
    }

    [Theory]
    [InlineData(222, 200, 400, 300, WindowDragKind.Move)] // same size elsewhere
    [InlineData(200, 200, 411, 300, WindowDragKind.Resize)] // right border: left edge kept
    [InlineData(190, 200, 410, 300, WindowDragKind.Resize)] // left border: right edge kept
    [InlineData(200, 220, 400, 280, WindowDragKind.Resize)] // top border: bottom edge kept
    [InlineData(500, 340, 800, 600, WindowDragKind.Move)] // snapped window restored under the pointer
    public void FromFirstChange_TellsMoveFromResize(double x, double y, double width, double height, WindowDragKind expected) =>
        Assert.Equal(expected, WindowDragKinds.FromFirstChange(new PhysicalRect(200, 200, 400, 300), new PhysicalRect(x, y, width, height)));

    [Fact]
    public void Options_Normalized_RepairsHandEditedValues()
    {
        var odd = new WindowDragOptions { Action = (WindowDragAction)42, LowerLevel = double.NaN };
        var tooHigh = new WindowDragOptions { LowerLevel = 99 };

        Assert.Equal(WindowDragOptions.Default, odd.Normalized());
        Assert.Equal(8, tooHigh.Normalized().LowerLevel);
        Assert.Equal(2.5, new WindowDragOptions { LowerLevel = 2.3 }.Normalized().LowerLevel);
        Assert.Equal(1.5, new WindowDragOptions { LowerLevel = 1 }.Normalized().LowerLevel);
        Assert.Equal(WindowDragAction.Lift, WindowDragOptions.Default.Action);
        Assert.True(WindowDragOptions.Default.OnMove && WindowDragOptions.Default.OnResize);
        Assert.Equal(2, WindowDragOptions.Default.LowerLevel);
        Assert.True(WindowDragOptions.Default.CanAct);
        Assert.False((WindowDragOptions.Default with { Action = WindowDragAction.Unchanged }).CanAct);
        Assert.False((WindowDragOptions.Default with { OnMove = false, OnResize = false }).CanAct);
    }
}
