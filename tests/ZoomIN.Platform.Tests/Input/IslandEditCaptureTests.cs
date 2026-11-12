using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.Tests.Input;

/// <summary>
/// The capturing hook mode of the island edit mode (M19 B1): only while it runs are clicks, wheel,
/// Esc and Del taken; a swallowed press keeps its release; moves always pass. Fed directly, as the
/// hook would, without installing a hook.
/// </summary>
public sealed class IslandEditCaptureTests
{
    private const uint WheelUp = 120u << 16;

    [Fact]
    public void Outside_TheEditMode_NothingIsTaken()
    {
        var capture = new IslandEditCapture(_ => false);

        Assert.False(capture.OnMouse(IslandEditCapture.LeftDown, new(10, 10), 0));
        Assert.False(capture.OnMouse(IslandEditCapture.LeftUp, new(10, 10), 0));
        Assert.False(capture.OnMouse(IslandEditCapture.Wheel, new(10, 10), WheelUp));
        Assert.False(capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1000));
        Assert.False(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false, 1000));
    }

    [Fact]
    public void While_Editing_ClicksWheelAndKeys_AreTakenAndHandedOn_InOrder()
    {
        var capture = new IslandEditCapture(_ => false);
        var handler = new RecordingIslandHandler();
        capture.Start(handler);

        Assert.True(capture.OnMouse(IslandEditCapture.LeftDown, new(10, 20), 0));
        Assert.False(capture.OnMouse(IslandEditCapture.MouseMove, new(30, 40), 0));
        Assert.True(capture.OnMouse(IslandEditCapture.LeftUp, new(50, 60), 0));
        Assert.True(capture.OnMouse(IslandEditCapture.Wheel, new(50, 60), WheelUp));
        Assert.True(capture.OnMouse(IslandEditCapture.RightDown, new(50, 60), 0));
        Assert.True(capture.OnMouse(IslandEditCapture.RightUp, new(50, 60), 0));
        Assert.True(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false, 1000));
        Assert.True(capture.OnKey(IslandEditCapture.DeleteKey, down: false, up: true, 1000));
        Assert.True(capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1000));

        handler.WaitFor(6);
        Assert.Equal(["press 10,20", "move 30,40", "release 50,60", "wheel 1 50,60", "key Delete", "key Escape"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void OtherKeys_AlwaysPass()
    {
        var capture = new IslandEditCapture(_ => false);
        capture.Start(new RecordingIslandHandler());

        Assert.False(capture.OnKey(0x41, down: true, up: false, 1000));
        Assert.False(capture.OnKey(0x41, down: false, up: true, 1000));
        capture.Stop();
    }

    [Fact]
    public void ReleaseAfterTheEnd_OfAPressTakenBefore_IsStillTaken()
    {
        var capture = new IslandEditCapture(_ => false);
        capture.Start(new RecordingIslandHandler());
        capture.OnMouse(IslandEditCapture.LeftDown, new(1, 1), 0);
        capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1000);

        capture.Stop();

        Assert.True(capture.OnMouse(IslandEditCapture.LeftUp, new(1, 1), 0));
        Assert.True(capture.OnKey(IslandEditCapture.EscapeKey, down: false, up: true, 1000));
        Assert.False(capture.OnMouse(IslandEditCapture.LeftUp, new(1, 1), 0));
    }

    [Fact]
    public void PressBeforeTheStart_KeepsItsReleaseForTheApplication()
    {
        var capture = new IslandEditCapture(_ => false);
        capture.OnMouse(IslandEditCapture.LeftDown, new(1, 1), 0);
        capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false, 1000);

        capture.Start(new RecordingIslandHandler());

        Assert.False(capture.OnMouse(IslandEditCapture.LeftUp, new(1, 1), 0));
        Assert.False(capture.OnKey(IslandEditCapture.DeleteKey, down: false, up: true, 1000));
        capture.Stop();
    }

    [Fact]
    public void KeyRepeat_IsTaken_ButHandedOnOnce()
    {
        var capture = new IslandEditCapture(_ => false);
        var handler = new RecordingIslandHandler();
        capture.Start(handler);

        for (var i = 0; i < 5; i++)
        {
            Assert.True(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false, 1000));
        }

        capture.OnKey(IslandEditCapture.DeleteKey, down: false, up: true, 1000);
        capture.OnMouse(IslandEditCapture.LeftDown, new(0, 0), 0);
        handler.WaitFor(2);
        Assert.Equal(["key Delete", "press 0,0"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void FailingHandler_DoesNotStopTheWorker()
    {
        var capture = new IslandEditCapture(_ => false);
        var handler = new RecordingIslandHandler { FailOnPress = true };
        capture.Start(handler);

        capture.OnMouse(IslandEditCapture.LeftDown, new(0, 0), 0);
        capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1000);

        handler.WaitFor(1);
        Assert.Equal(["key Escape"], handler.Calls);
        capture.Stop();
    }
}
