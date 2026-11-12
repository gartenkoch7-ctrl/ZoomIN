using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Pointer;
using ZoomIN.Platform.PointerConstraint;

namespace ZoomIN.Platform.Tests.PointerConstraint;

/// <summary>
/// The axis lock key (A6) without a real hook: left and right keys apart, key repeat once, and a
/// missed release never keeps the lock on.
/// </summary>
public sealed class HoldKeyWatchTests : IDisposable
{
    private const int LeftShift = 0xA0;
    private const int RightShift = 0xA1;

    private readonly List<bool> _changes = [];
    private readonly HashSet<int> _down = [];
    private readonly StoppedTimers _time = new();
    private readonly Win32HoldKeyWatch _watch;

    public HoldKeyWatchTests()
    {
        _watch = new Win32HoldKeyWatch(NullLogger.Instance, _time, _down.Contains, installHook: false);
    }

    public void Dispose() => _watch.Dispose();

    [Theory]
    [InlineData(AxisLockKey.LeftShift, 0xA0)]
    [InlineData(AxisLockKey.RightShift, 0xA1)]
    [InlineData(AxisLockKey.LeftControl, 0xA2)]
    [InlineData(AxisLockKey.RightControl, 0xA3)]
    [InlineData(AxisLockKey.LeftAlt, 0xA4)]
    [InlineData(AxisLockKey.RightAlt, 0xA5)]
    public void Keys_TellLeftAndRightApart(AxisLockKey key, int virtualKey) =>
        Assert.Equal(virtualKey, HoldKeyCodes.VirtualKey(key));

    [Fact]
    public void PressAndRelease_AreReportedOnce()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);

        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);
        _watch.OnKey(LeftShift, 0x2A, down: true, up: false); // key repeat
        _watch.OnKey(LeftShift, 0x2A, down: false, up: true);

        Assert.Equal([true, false], _changes);
    }

    [Fact]
    public void OtherSide_IsIgnored()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);

        _watch.OnKey(RightShift, 0x2A, down: true, up: false);

        Assert.Empty(_changes);
    }

    [Fact]
    public void MissedRelease_IsCaughtByTheCheck()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);
        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);

        // The key is not physically down (released on the lock screen).
        _watch.Check();

        Assert.Equal([true, false], _changes);
    }

    [Fact]
    public void HeldKey_SurvivesTheCheck()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);
        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);
        _down.Add(LeftShift);

        _watch.Check();
        _watch.Check();
        _watch.Check();

        Assert.Equal([true], _changes);
    }

    [Fact]
    public void Stop_ReleasesAHeldKey()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);
        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);

        _watch.Stop();
        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);

        Assert.Equal([true, false], _changes);
    }

    [Fact]
    public void Restart_ReleasesAndWatchesTheNewKey()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);
        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);

        _watch.Start(AxisLockKey.RightShift, _changes.Add);
        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);
        _watch.OnKey(RightShift, 0x2A, down: true, up: false);

        Assert.Equal([true, false, true], _changes);
    }

    [Fact]
    public void ThrowingHandler_DoesNotEscapeTheHook()
    {
        _watch.Start(AxisLockKey.LeftShift, _ => throw new InvalidOperationException("broken"));

        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);
        _watch.Check();
    }

    [Fact]
    public void Start_CreatesTheCheckTimerOnce()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);
        _watch.Start(AxisLockKey.RightAlt, _changes.Add);

        Assert.Equal(1, _time.Created);
        Assert.Equal(Timeout.InfiniteTimeSpan, _time.Period);
    }

    [Fact]
    public void CheckTimer_RunsOnlyWhileHeld()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);

        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);
        Assert.Equal(Win32HoldKeyWatch.CheckInterval, _time.Period);

        // Released: one last check (reports failures), no repeat.
        _watch.OnKey(LeftShift, 0x2A, down: false, up: true);
        Assert.Equal(Timeout.InfiniteTimeSpan, _time.Period);
    }

    [Fact]
    public void AltGrFakeControl_IsNotAHeldControl()
    {
        _watch.Start(AxisLockKey.LeftControl, _changes.Add);

        _watch.OnKey(0xA2, HoldKeyCodes.AltGrFakeControlScanCode, down: true, up: false);
        Assert.Empty(_changes);

        _watch.OnKey(0xA2, 0x1D, down: true, up: false);
        Assert.Equal([true], _changes);
    }

    [Fact]
    public void Reset_ReleasesAndKeepsWatching()
    {
        _watch.Start(AxisLockKey.LeftShift, _changes.Add);
        _watch.OnKey(LeftShift, 0x2A, down: true, up: false);

        _watch.Reset();
        _watch.OnKey(LeftShift, 0x2A, down: true, up: false); // the next repeat of a key still held

        Assert.Equal([true, false, true], _changes);
    }

    [Fact]
    public void RealHook_InstallsAndStops()
    {
        // The real keyboard hook only watches: nothing is pressed or swallowed here.
        using var watch = new Win32HoldKeyWatch(NullLogger<Win32HoldKeyWatch>.Instance, _time);
        watch.Start(AxisLockKey.RightControl, _changes.Add);
        watch.Stop();

        Assert.Empty(_changes);
    }

    private sealed class StoppedTimers : TimeProvider
    {
        public int Created { get; private set; }

        public TimeSpan Period { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Created++;
            Period = period;
            return new Stopped(this);
        }

        private sealed class Stopped(StoppedTimers owner) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                owner.Period = period;
                return true;
            }

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
