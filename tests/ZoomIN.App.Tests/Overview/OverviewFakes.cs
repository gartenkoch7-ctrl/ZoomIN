using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.Overview;

/// <summary>Records what the overview asks of the keyboard hook; tests send the signals.</summary>
internal sealed class FakeOverviewKeys : IOverviewKeys
{
    public long WatchId { get; private set; }

    public HotkeyGesture Gesture { get; private set; }

    public IOverviewKeyHandler? Handler { get; private set; }

    public bool Watching => WatchId != 0;

    public void Watch(long watchId, HotkeyGesture gesture, IOverviewKeyHandler handler)
    {
        WatchId = watchId;
        Gesture = gesture;
        Handler = handler;
    }

    public void Stop(long watchId)
    {
        if (WatchId == watchId)
        {
            WatchId = 0;
        }
    }
}

internal sealed class RecordingAnnouncer : IAnnouncer
{
    public List<string> Messages { get; } = [];

    public void Announce(string message)
    {
        lock (Messages)
        {
            Messages.Add(message);
        }
    }
}

/// <summary>Two monitors side by side, 1920×1080 each.</summary>
internal sealed class TwoMonitors : IMonitorProvider
{
    public const string First = @"\\.\DISPLAY1";
    public const string Second = @"\\.\DISPLAY2";

    public IReadOnlyList<string> MonitorIds() => [First, Second];

    public string MonitorAt(PhysicalPoint point) => point.X >= 1920 ? Second : First;

    public PhysicalRect? BoundsOf(string monitorId) => monitorId switch
    {
        First => new PhysicalRect(0, 0, 1920, 1080),
        Second => new PhysicalRect(1920, 0, 1920, 1080),
        _ => null,
    };
}

internal sealed class MovableCursor : ICursorSource
{
    public PhysicalPoint Position { get; set; } = new(960, 540);

    public bool TryGetPosition(out PhysicalPoint position)
    {
        position = Position;
        return true;
    }
}

/// <summary>A clock the test moves by hand.</summary>
internal sealed class ManualClock : TimeProvider
{
    private long _ticks = 1_000_000;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
