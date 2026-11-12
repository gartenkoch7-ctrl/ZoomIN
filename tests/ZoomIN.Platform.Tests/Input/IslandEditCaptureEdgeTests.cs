using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.Tests.Input;

/// <summary>The capturing hook mode of the island edit mode (M19 B1): lost releases, keys held or with modifiers, order, the end of a run.</summary>
public sealed class IslandEditCaptureEdgeTests
{
    [Fact]
    public void LostRelease_DoesNotSwallowTheNextClickOrKey_AfterTheEnd()
    {
        var capture = new IslandEditCapture(_ => false);
        capture.Start(new RecordingIslandHandler());
        capture.OnMouse(IslandEditCapture.LeftDown, new(1, 1), 0);
        capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1000);
        capture.Stop(); // e.g. Win+L: the hooks never see both releases

        Assert.False(capture.OnMouse(IslandEditCapture.LeftDown, new(1, 1), 0));
        Assert.False(capture.OnMouse(IslandEditCapture.LeftUp, new(1, 1), 0));
        Assert.False(capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 60_000));
        Assert.False(capture.OnKey(IslandEditCapture.EscapeKey, down: false, up: true, 60_100));
    }

    [Fact]
    public void EscAndDelWithAModifier_StayTheSystems()
    {
        var shift = false;
        var capture = new IslandEditCapture(key => shift && key == 0x10);
        var handler = new RecordingIslandHandler();
        capture.Start(handler);

        shift = true;
        Assert.False(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false, 1000));
        Assert.False(capture.OnKey(IslandEditCapture.DeleteKey, down: false, up: true, 1100));
        shift = false;
        Assert.True(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false, 2000));

        handler.WaitFor(1);
        Assert.Equal(["key Delete"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void KeyHeldAtTheStart_KeepsItsRepeatsAndRelease_TheNextPressIsTaken()
    {
        var deleteDown = true;
        var capture = new IslandEditCapture(key => deleteDown && key == IslandEditCapture.DeleteKey);
        var handler = new RecordingIslandHandler();
        capture.Start(handler);
        deleteDown = false;

        Assert.False(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false, 1000));
        Assert.False(capture.OnKey(IslandEditCapture.DeleteKey, down: false, up: true, 1050));
        Assert.True(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false, 2000));

        handler.WaitFor(1);
        Assert.Equal(["key Delete"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void TouchpadWheel_PartsOfANotch_AddUpToWholeSteps()
    {
        var capture = new IslandEditCapture(_ => false);
        var handler = new RecordingIslandHandler();
        capture.Start(handler);

        for (var i = 0; i < 6; i++)
        {
            Assert.True(capture.OnMouse(IslandEditCapture.Wheel, new(5, 5), 40u << 16)); // a third of a notch each
        }

        handler.WaitFor(2);
        Thread.Sleep(50);
        Assert.Equal(["wheel 1 5,5", "wheel 1 5,5"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void KeyHeldAtTheStart_WhoseReleaseWasLost_TheNextPressAfterTheGapIsTaken()
    {
        var escDown = true;
        var capture = new IslandEditCapture(key => escDown && key == IslandEditCapture.EscapeKey);
        var handler = new RecordingIslandHandler();
        capture.Start(handler);
        escDown = false;

        Assert.False(capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1000)); // a repeat: passes
        Assert.True(capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 10_000)); // release was lost

        handler.WaitFor(1);
        Assert.Equal(["key Escape"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void CtrlWheel_StaysTheZooms()
    {
        var ctrl = true;
        var capture = new IslandEditCapture(key => ctrl && key == 0x11);
        capture.Start(new RecordingIslandHandler());

        Assert.False(capture.OnMouse(IslandEditCapture.Wheel, new(5, 5), 120u << 16));
        ctrl = false;
        Assert.True(capture.OnMouse(IslandEditCapture.Wheel, new(5, 5), 120u << 16));
        capture.Stop();
    }

    [Fact]
    public void EscSwallowedThenStillHeldAtTheNextStart_KeepsItsReleaseSwallowed_AndTheNextPressCounts()
    {
        var escDown = false;
        var capture = new IslandEditCapture(key => escDown && key == IslandEditCapture.EscapeKey);
        var handler = new RecordingIslandHandler();
        capture.Start(handler);
        Assert.True(capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1000)); // ends the run
        capture.Stop();

        escDown = true;
        var next = new RecordingIslandHandler();
        capture.Start(next); // started again while Esc is still down
        escDown = false;

        Assert.True(capture.OnKey(IslandEditCapture.EscapeKey, down: false, up: true, 1200));
        Assert.True(capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1500));
        next.WaitFor(1);
        Assert.Equal(["key Escape"], next.Calls);
        capture.Stop();
    }

    [Fact]
    public void ExtraButtons_KeepTheirReleasesApart()
    {
        const uint First = 1u << 16;
        const uint Second = 2u << 16;
        var capture = new IslandEditCapture(_ => false);
        Assert.False(capture.OnMouse(IslandEditCapture.ExtraDown, new(1, 1), First)); // held before the start
        capture.Start(new RecordingIslandHandler());

        Assert.True(capture.OnMouse(IslandEditCapture.ExtraDown, new(1, 1), Second));
        Assert.False(capture.OnMouse(IslandEditCapture.ExtraUp, new(1, 1), First));
        Assert.True(capture.OnMouse(IslandEditCapture.ExtraUp, new(1, 1), Second));
        capture.Stop();
    }

    [Fact]
    public void SwappedButtons_ThePhysicalRightButtonDrags()
    {
        var capture = new IslandEditCapture(_ => false, () => true);
        var handler = new RecordingIslandHandler();
        capture.Start(handler);

        Assert.True(capture.OnMouse(IslandEditCapture.LeftDown, new(1, 1), 0));
        Assert.True(capture.OnMouse(IslandEditCapture.LeftUp, new(1, 1), 0));
        Assert.True(capture.OnMouse(IslandEditCapture.RightDown, new(5, 5), 0));
        Assert.True(capture.OnMouse(IslandEditCapture.RightUp, new(9, 9), 0));

        handler.WaitFor(2);
        Assert.Equal(["press 5,5", "release 9,9"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void MoveAfterAPress_ReachesTheHandlerAfterIt()
    {
        var capture = new IslandEditCapture(_ => false);
        using var gate = new ManualResetEventSlim(false);
        var blocking = new RecordingIslandHandler { Gate = gate };
        capture.Start(blocking);
        capture.OnMouse(IslandEditCapture.LeftDown, new(0, 0), 0); // the worker waits in this one
        Assert.True(SpinWait.SpinUntil(() => blocking.Waiting, TimeSpan.FromSeconds(5)));

        capture.OnMouse(IslandEditCapture.MouseMove, new(1, 1), 0);
        capture.OnMouse(IslandEditCapture.LeftUp, new(2, 2), 0);
        capture.OnMouse(IslandEditCapture.MouseMove, new(3, 3), 0);
        gate.Set();

        blocking.WaitFor(4);
        Assert.Equal(["press 0,0", "move 1,1", "release 2,2", "move 3,3"], blocking.Calls);
        capture.Stop();
    }

    [Fact]
    public void InputsStillQueued_AtStop_AreDropped()
    {
        var capture = new IslandEditCapture(_ => false);
        using var gate = new ManualResetEventSlim(false);
        var handler = new RecordingIslandHandler { Gate = gate };
        capture.Start(handler);

        capture.OnMouse(IslandEditCapture.LeftDown, new(1, 1), 0); // the worker waits in this one
        Assert.True(SpinWait.SpinUntil(() => handler.Waiting, TimeSpan.FromSeconds(5)));
        capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false, 1000);
        capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1000);
        capture.Stop();
        gate.Set();

        handler.WaitFor(1);
        Thread.Sleep(100);
        Assert.Equal(["press 1,1"], handler.Calls);
    }
}
