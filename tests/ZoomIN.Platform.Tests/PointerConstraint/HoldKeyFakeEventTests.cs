using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Pointer;
using ZoomIN.Platform.PointerConstraint;

namespace ZoomIN.Platform.Tests.PointerConstraint;

/// <summary>Key events Windows makes up (NumLock Shift, AltGr Ctrl) versus injected keys that count (A6).</summary>
public sealed class HoldKeyFakeEventTests : IDisposable
{
    private const int LeftShift = 0xA0;
    private const int RightShift = 0xA1;

    private readonly List<bool> _changes = [];
    private readonly HashSet<int> _down = [];
    private readonly Win32HoldKeyWatch _watch;

    public HoldKeyFakeEventTests()
    {
        _watch = new Win32HoldKeyWatch(NullLogger.Instance, new NoTimers(), _down.Contains, installHook: false);
    }

    public void Dispose() => _watch.Dispose();

    [Fact]
    public void FakeShiftUp_IsNoMissedRelease()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);
        _watch.OnKey(LeftShift, 0x2A, extended: false, down: true, up: false);

        // A numpad arrow held with NumLock on: Windows' fake Shift up sets the key state to up.
        _watch.OnKey(LeftShift, 0x2A, extended: true, down: false, up: true);
        _watch.Check();
        Assert.Equal([true], _changes);

        // The fake Shift down after the arrow: the check is honest again.
        _watch.OnKey(LeftShift, 0x2A, extended: true, down: true, up: false);
        _watch.Check();
        Assert.Equal([true, false], _changes);
    }

    [Fact]
    public void FakeUpThenMissedRealRelease_IsReleasedAfterAFewChecks()
    {
        _watch.Start(AxisLockKey.LeftControl, _changes.Add);
        _watch.OnKey(0xA2, 0x1D, extended: false, down: true, up: false);
        _watch.OnKey(0xA2, HoldKeyCodes.AltGrFakeControlScanCode, extended: false, down: false, up: true);

        // The real release is never seen (e.g. over an elevated window): never locked for good.
        _watch.Check();
        _watch.Check();
        _watch.Check();
        Assert.Equal([true], _changes);
        _watch.Check();
        Assert.Equal([true, false], _changes);
    }

    [Fact]
    public void FakeDownAfterAForcedRelease_EngagesAgain()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);
        _watch.OnKey(LeftShift, 0x2A, extended: false, down: true, up: false);
        _watch.OnKey(LeftShift, 0x2A, extended: true, down: false, up: true);
        for (var i = 0; i < 4; i++)
        {
            _watch.Check(); // numpad arrow held for 2 s: released to be safe
        }

        // The arrow is let go and Windows restores the Shift it took away: still held.
        _watch.OnKey(LeftShift, 0x2A, extended: true, down: true, up: false);

        Assert.Equal([true, false, true], _changes);
    }

    [Fact]
    public void InjectedExtendedRightShift_Counts()
    {
        _watch.Start(AxisLockKey.RightShift, _changes.Add);

        // A remapper or accessibility tool sending the right Shift with the extended flag.
        _watch.OnKey(RightShift, 0x36, extended: true, down: true, up: false, injected: true);
        _watch.OnKey(RightShift, 0x36, extended: true, down: false, up: true, injected: true);

        Assert.Equal([true, false], _changes);
    }

    [Fact]
    public void InjectedNeutralShift_CountsAsLeftShift()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);

        _watch.OnKey(0x10, 0x2A, extended: false, down: true, up: false, injected: true);

        Assert.Equal([true], _changes);
    }

    [Theory]
    [InlineData(0x10, 0x2Au, false, 0xA0)]
    [InlineData(0x10, 0x36u, false, 0xA1)]
    [InlineData(0x11, 0x1Du, false, 0xA2)]
    [InlineData(0x11, 0x1Du, true, 0xA3)]
    [InlineData(0x12, 0x38u, false, 0xA4)]
    [InlineData(0x12, 0x38u, true, 0xA5)]
    [InlineData(0xA0, 0x2Au, false, 0xA0)]
    public void Sided_MapsNeutralModifiers(int virtualKey, uint scanCode, bool extended, int sided) =>
        Assert.Equal(sided, HoldKeyCodes.Sided(virtualKey, scanCode, extended));

    [Theory]
    [InlineData(0x21Du, false, false, true)]
    [InlineData(0x22Au, false, false, true)]
    [InlineData(0x2Au, true, false, true)]
    [InlineData(0x36u, true, false, false)] // right Shift may be extended for real
    [InlineData(0x36u, true, true, false)]
    [InlineData(0x2Au, false, false, false)]
    [InlineData(0x1Du, true, false, false)] // right Ctrl is extended for real
    public void IsFake_TellsMadeUpEventsApart(uint scanCode, bool extended, bool injected, bool fake) =>
        Assert.Equal(fake, HoldKeyCodes.IsFake(scanCode, extended, injected));

    // The tests call Check themselves; a real timer would race them.
    private sealed class NoTimers : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new Idle();

        private sealed class Idle : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
