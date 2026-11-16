using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Markers;
using ZoomIN.App.Tests.Overview;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Tests.Markers;

/// <summary>Zoom off while the hold key is held, back on release; manual clock and timer (ADR-0025).</summary>
public sealed class HoldKeyZoomTests : IDisposable
{
    private readonly TimerClock _clock = new();
    private readonly MarkerActivity _activity;
    private readonly HoldKeyZoom _zoom;
    private readonly ZoomModel _model;

    public HoldKeyZoomTests()
    {
        _activity = new MarkerActivity(_clock);
        var initial = new AppSettings
        {
            ZoomOn = true,
            MonitorZoomLevels = new Dictionary<string, double> { [TwoMonitors.First] = 2, [TwoMonitors.Second] = 2 },
        };
        var settings = MarkerTestSettings.Create(initial, _activity);
        _model = settings.Models.Zoom;
        _zoom = new HoldKeyZoom(settings, new TwoMonitors(), _clock, NullLogger<HoldKeyZoom>.Instance);
        _zoom.Start();
    }

    public void Dispose() => _zoom.Dispose();

    [Fact]
    public void HeldLongEnough_EveryMonitorAtOneTimes_ReleaseGivesTheZoomBack()
    {
        _activity.HoldKeyDown();
        Assert.True(_clock.Timer!.Armed);
        _zoom.Check(); // too early: waits for the minimum time
        Assert.Null(Level(TwoMonitors.First));

        _clock.Advance(MarkerVisibility.MinHoldTime);
        _activity.HoldKeyDown(); // key repeat
        _zoom.Check();
        Assert.Equal(ZoomLimits.Min, Level(TwoMonitors.First));
        Assert.Equal(ZoomLimits.Min, Level(TwoMonitors.Second));
        Assert.Equal(ZoomLimits.Min, _model.EffectiveTargetFor(TwoMonitors.First));

        _activity.HoldKeyUp();
        _zoom.Check();
        Assert.Null(Level(TwoMonitors.First));
        Assert.Null(Level(TwoMonitors.Second));
    }

    [Fact]
    public void ShortTapOrTyping_ChangesNothing()
    {
        _activity.HoldKeyDown();
        _activity.OtherKeyDown(); // Shift for a capital letter
        _clock.Advance(MarkerVisibility.MinHoldTime);
        _zoom.Check();

        Assert.Null(Level(TwoMonitors.First));
    }

    [Fact]
    public void MissedRelease_EndsAfterTheTimeout()
    {
        HoldLongEnough();
        Assert.NotNull(Level(TwoMonitors.First));

        _clock.Advance(MarkerVisibility.HoldTimeout);
        _zoom.Check();

        Assert.Null(Level(TwoMonitors.First));
    }

    [Fact]
    public void Docked_ChangesNothing()
    {
        _model.SetMode(ZoomMode.Docked);

        HoldLongEnough();

        Assert.Null(Level(TwoMonitors.First));
    }

    [Fact]
    public void Dispose_ReleasesAHeldLevel()
    {
        HoldLongEnough();

        _zoom.Dispose();

        Assert.Null(Level(TwoMonitors.First));
        Assert.True(_clock.Timer!.Disposed);
    }

    private void HoldLongEnough()
    {
        _activity.HoldKeyDown();
        _clock.Advance(MarkerVisibility.MinHoldTime);
        _zoom.Check();
    }

    private double? Level(string monitor) => _model.Temporary.LevelFor(TemporaryZoomOwner.HoldKey, monitor);

    /// <summary>A clock the test moves by hand; its one timer is fired by calling Check.</summary>
    private sealed class TimerClock : TimeProvider
    {
        private long _ticks = 1_000_000;

        public ManualTimer? Timer { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            Timer = new ManualTimer();
    }

    private sealed class ManualTimer : ITimer
    {
        public bool Armed { get; private set; }

        public bool Disposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Armed = dueTime != Timeout.InfiniteTimeSpan;
            return !Disposed;
        }

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
