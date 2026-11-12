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
        capture.OnMouse(IslandEditCapture.LeftDown, 1, 1, 0);
        capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false, 1000);
        capture.Stop(); // e.g. Win+L: the hooks never see both releases

        Assert.False(capture.OnMouse(IslandEditCapture.LeftDown, 1, 1, 0));
        Assert.False(capture.OnMouse(IslandEditCapture.LeftUp, 1, 1, 0));
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
    public void MoveAfterAPress_ReachesTheHandlerAfterIt()
    {
        var capture = new IslandEditCapture(_ => false);
        using var gate = new ManualResetEventSlim(false);
        var blocking = new RecordingIslandHandler { Gate = gate };
        capture.Start(blocking);
        capture.OnMouse(IslandEditCapture.LeftDown, 0, 0, 0); // the worker waits in this one
        Assert.True(SpinWait.SpinUntil(() => blocking.Waiting, TimeSpan.FromSeconds(5)));

        capture.OnMouse(IslandEditCapture.MouseMove, 1, 1, 0);
        capture.OnMouse(IslandEditCapture.LeftUp, 2, 2, 0);
        capture.OnMouse(IslandEditCapture.MouseMove, 3, 3, 0);
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

        capture.OnMouse(IslandEditCapture.LeftDown, 1, 1, 0); // the worker waits in this one
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
