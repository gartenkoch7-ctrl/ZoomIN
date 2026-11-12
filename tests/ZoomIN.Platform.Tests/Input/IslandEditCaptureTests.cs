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
        var capture = new IslandEditCapture();

        Assert.False(capture.OnMouse(IslandEditCapture.LeftDown, 10, 10, 0));
        Assert.False(capture.OnMouse(IslandEditCapture.LeftUp, 10, 10, 0));
        Assert.False(capture.OnMouse(IslandEditCapture.Wheel, 10, 10, WheelUp));
        Assert.False(capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false));
        Assert.False(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false));
    }

    [Fact]
    public void While_Editing_ClicksWheelAndKeys_AreTakenAndHandedOn_InOrder()
    {
        var capture = new IslandEditCapture();
        var handler = new RecordingHandler();
        capture.Start(handler);

        Assert.True(capture.OnMouse(IslandEditCapture.LeftDown, 10, 20, 0));
        Assert.False(capture.OnMouse(IslandEditCapture.MouseMove, 30, 40, 0));
        Assert.True(capture.OnMouse(IslandEditCapture.LeftUp, 50, 60, 0));
        Assert.True(capture.OnMouse(IslandEditCapture.Wheel, 50, 60, WheelUp));
        Assert.True(capture.OnMouse(IslandEditCapture.RightDown, 50, 60, 0));
        Assert.True(capture.OnMouse(IslandEditCapture.RightUp, 50, 60, 0));
        Assert.True(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false));
        Assert.True(capture.OnKey(IslandEditCapture.DeleteKey, down: false, up: true));
        Assert.True(capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false));

        handler.WaitFor(6);
        Assert.Equal(["press 10,20", "move 30,40", "release 50,60", "wheel 1 50,60", "key Delete", "key Escape"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void OtherKeys_AlwaysPass()
    {
        var capture = new IslandEditCapture();
        capture.Start(new RecordingHandler());

        Assert.False(capture.OnKey(0x41, down: true, up: false));
        Assert.False(capture.OnKey(0x41, down: false, up: true));
        capture.Stop();
    }

    [Fact]
    public void ReleaseAfterTheEnd_OfAPressTakenBefore_IsStillTaken()
    {
        var capture = new IslandEditCapture();
        capture.Start(new RecordingHandler());
        capture.OnMouse(IslandEditCapture.LeftDown, 1, 1, 0);
        capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false);

        capture.Stop();

        Assert.True(capture.OnMouse(IslandEditCapture.LeftUp, 1, 1, 0));
        Assert.True(capture.OnKey(IslandEditCapture.EscapeKey, down: false, up: true));
        Assert.False(capture.OnMouse(IslandEditCapture.LeftUp, 1, 1, 0));
    }

    [Fact]
    public void PressBeforeTheStart_KeepsItsReleaseForTheApplication()
    {
        var capture = new IslandEditCapture();
        capture.OnMouse(IslandEditCapture.LeftDown, 1, 1, 0);
        capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false);

        capture.Start(new RecordingHandler());

        Assert.False(capture.OnMouse(IslandEditCapture.LeftUp, 1, 1, 0));
        Assert.False(capture.OnKey(IslandEditCapture.DeleteKey, down: false, up: true));
        capture.Stop();
    }

    [Fact]
    public void KeyRepeat_IsTaken_ButHandedOnOnce()
    {
        var capture = new IslandEditCapture();
        var handler = new RecordingHandler();
        capture.Start(handler);

        for (var i = 0; i < 5; i++)
        {
            Assert.True(capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false));
        }

        capture.OnKey(IslandEditCapture.DeleteKey, down: false, up: true);
        capture.OnMouse(IslandEditCapture.LeftDown, 0, 0, 0);
        handler.WaitFor(2);
        Assert.Equal(["key Delete", "press 0,0"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void FailingHandler_DoesNotStopTheWorker()
    {
        var capture = new IslandEditCapture();
        var handler = new RecordingHandler { FailOnPress = true };
        capture.Start(handler);

        capture.OnMouse(IslandEditCapture.LeftDown, 0, 0, 0);
        capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false);

        handler.WaitFor(1);
        Assert.Equal(["key Escape"], handler.Calls);
        capture.Stop();
    }

    [Fact]
    public void InputsStillQueued_AtStop_AreDropped()
    {
        var capture = new IslandEditCapture();
        using var gate = new ManualResetEventSlim(false);
        var handler = new RecordingHandler { Gate = gate };
        capture.Start(handler);

        capture.OnMouse(IslandEditCapture.LeftDown, 1, 1, 0); // the worker waits in this one
        Assert.True(SpinWait.SpinUntil(() => handler.Waiting, TimeSpan.FromSeconds(5)));
        capture.OnKey(IslandEditCapture.DeleteKey, down: true, up: false);
        capture.OnKey(IslandEditCapture.EscapeKey, down: true, up: false);
        capture.Stop();
        gate.Set();

        handler.WaitFor(1);
        Thread.Sleep(100);
        Assert.Equal(["press 1,1"], handler.Calls);
    }

    private sealed class RecordingHandler : IIslandEditInputHandler
    {
        private readonly List<string> _calls = [];

        public bool FailOnPress { get; init; }

        /// <summary>The press waits for it (to queue more input behind it).</summary>
        public ManualResetEventSlim? Gate { get; init; }

        public bool Waiting { get; private set; }

        public IReadOnlyList<string> Calls
        {
            get
            {
                lock (_calls)
                {
                    return [.. _calls];
                }
            }
        }

        public void WaitFor(int count) =>
            Assert.True(SpinWait.SpinUntil(() => Calls.Count >= count, TimeSpan.FromSeconds(5)), "The worker handed on too little.");

        public void Pressed(PhysicalPoint point)
        {
            if (FailOnPress)
            {
                throw new InvalidOperationException("test");
            }

            if (Gate is not null)
            {
                Waiting = true;
                Gate.Wait(TimeSpan.FromSeconds(5));
            }

            Add($"press {point.X},{point.Y}");
        }

        public void Moved(PhysicalPoint point) => Add($"move {point.X},{point.Y}");

        public void Released(PhysicalPoint point) => Add($"release {point.X},{point.Y}");

        public void Wheel(double notches, PhysicalPoint point) => Add($"wheel {notches} {point.X},{point.Y}");

        public void Key(IslandEditKey key) => Add($"key {key}");

        private void Add(string call)
        {
            lock (_calls)
            {
                _calls.Add(call);
            }
        }
    }
}
