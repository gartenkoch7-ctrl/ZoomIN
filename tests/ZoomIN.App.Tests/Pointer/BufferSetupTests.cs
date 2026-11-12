using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Pointer;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.Pointer;

/// <summary>Monitors not readable mid display change (A6): the buffer rests and reads again later, never an old layout.</summary>
public sealed class BufferSetupTests
{
    private readonly EdgeBuffer _buffer = new(TimeProvider.System);
    private readonly SwitchableMonitors _monitors = new();
    private readonly CallbackClock _time = new();

    [Fact]
    public void NoMonitors_BufferRestsAndRetries()
    {
        using var setup = new BufferSetup(_buffer, _monitors, _time, NullLogger.Instance);
        setup.Configure(EdgeBufferLevel.Strong);
        _monitors.Readable = false;

        Assert.Equal(0, setup.Configure(null));
        Assert.Equal(new PhysicalPoint(1925, 10), _buffer.Filter(0, new PhysicalPoint(1919, 10), new PhysicalPoint(1925, 10)));

        _monitors.Readable = true;
        _time.Fire();
        Assert.Equal(new PhysicalPoint(1919, 10), _buffer.Filter(0, new PhysicalPoint(1919, 10), new PhysicalPoint(1925, 10)));
    }

    [Fact]
    public void RetryBudget_StartsAgainWithEveryRequest()
    {
        using var setup = new BufferSetup(_buffer, _monitors, _time, NullLogger.Instance);
        _monitors.Readable = false;
        setup.Configure(EdgeBufferLevel.Light);
        for (var i = 0; i < 20; i++)
        {
            _time.Fire();
        }

        // A later display change while Windows still enumerates: retried again once readable.
        setup.Configure(null);
        _monitors.Readable = true;
        _time.Fire();

        Assert.Equal(new PhysicalPoint(1919, 10), _buffer.Filter(0, new PhysicalPoint(1919, 10), new PhysicalPoint(1925, 10)));
    }

    [Fact]
    public void Off_NeedsNoRetry()
    {
        using var setup = new BufferSetup(_buffer, _monitors, _time, NullLogger.Instance);
        _monitors.Readable = false;

        setup.Configure(EdgeBufferLevel.Off);

        Assert.Null(_time.Callback);
    }

    private sealed class SwitchableMonitors : IMonitorProvider
    {
        public bool Readable { get; set; } = true;

        public IReadOnlyList<string> MonitorIds() => ["A", "B"];

        public string MonitorAt(PhysicalPoint point) => point.X >= 1920 ? "B" : "A";

        public PhysicalRect? BoundsOf(string monitorId) => !Readable ? null
            : monitorId == "A" ? new PhysicalRect(0, 0, 1920, 1080) : new PhysicalRect(1920, 0, 1920, 1080);
    }

    private sealed class CallbackClock : TimeProvider
    {
        public TimerCallback? Callback { get; private set; }

        public void Fire() => Callback?.Invoke(null);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Callback = callback;
            return new Idle();
        }

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
