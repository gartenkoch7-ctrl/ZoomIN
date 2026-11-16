namespace ZoomIN.App.Tests.Islands;

/// <summary>A time provider whose timers fire only when the test says so.</summary>
internal sealed class TimerClock : TimeProvider
{
    public List<ManualTimer> Timers { get; } = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, dueTime);
        Timers.Add(timer);
        return timer;
    }

    /// <summary>A timer the test fires by hand.</summary>
    internal sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        public TimeSpan Due { get; private set; } = due;

        public int Changes { get; private set; }

        public bool Disposed { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime;
            Changes++;
            return true;
        }

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
