using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.SecureDesktop;

/// <summary>
/// The start signal waits for Explorer (Q9 retest 2026-11-16: an autostart before the taskbar lost
/// its Win+U). Private key under HKCU, no real key presses.
/// </summary>
public sealed class EaseOfAccessNotifierShellTests : IDisposable
{
    private const string Parent = @"Software\ZoomIN.Tests.EaseOfAccessShell";

    private readonly string _path = Parent + @"\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey _root;
    private readonly FakeTray _tray = new();
    private int _winUCount;

    public EaseOfAccessNotifierShellTests() => _root = Registry.CurrentUser.CreateSubKey(_path);

    [Fact]
    public void TaskbarThere_SignalsAtOnce()
    {
        _tray.IsShown = true;

        Notifier().NotifyStartedWhenShellRuns(_tray, TimeSpan.Zero, TimeProvider.System);

        Assert.Equal(1, _winUCount);
    }

    [Fact]
    public void TaskbarLate_SignalsOnceItShows_OnlyOnce()
    {
        Notifier().NotifyStartedWhenShellRuns(_tray, TimeSpan.Zero, TimeProvider.System);
        Assert.Equal(0, _winUCount);

        _tray.ShowNow();
        WaitFor(() => Volatile.Read(ref _winUCount) == 1);

        // An Explorer restart shows the icon again: no second start signal.
        _tray.ShowNow();
        Thread.Sleep(50);
        Assert.Equal(1, Volatile.Read(ref _winUCount));
    }

    [Fact]
    public void ExitBeforeTheTaskbar_SendsNoSignalAtAll()
    {
        var notifier = Notifier();
        notifier.NotifyStartedWhenShellRuns(_tray, TimeSpan.Zero, TimeProvider.System);

        notifier.NotifyExiting();
        _tray.ShowNow();
        Thread.Sleep(50);

        Assert.Equal(0, Volatile.Read(ref _winUCount));
    }

    [Fact]
    public void ExitDuringTheSettlePause_SendsNoStartSignal()
    {
        var clock = new ManualClock();
        var notifier = Notifier();
        notifier.NotifyStartedWhenShellRuns(_tray, TimeSpan.FromSeconds(2), clock);
        _tray.ShowNow();

        notifier.NotifyExiting();
        clock.Fire(TimeSpan.FromSeconds(2));
        Thread.Sleep(50);

        Assert.Equal(0, Volatile.Read(ref _winUCount));
    }

    [Fact]
    public void SettlePause_IsWaitedFor()
    {
        var clock = new ManualClock();
        Notifier().NotifyStartedWhenShellRuns(_tray, TimeSpan.FromSeconds(2), clock);
        _tray.ShowNow();
        Thread.Sleep(50);
        Assert.Equal(0, Volatile.Read(ref _winUCount));

        clock.Fire(TimeSpan.FromSeconds(2));
        WaitFor(() => Volatile.Read(ref _winUCount) == 1);
    }

    [Fact]
    public void NoTrayIconForAMinute_SignalsAnyway_Once()
    {
        var clock = new ManualClock();
        Notifier().NotifyStartedWhenShellRuns(_tray, TimeSpan.FromSeconds(2), clock);

        clock.Fire(EaseOfAccessNotifier.ShellWaitLimit);
        WaitFor(() => Volatile.Read(ref _winUCount) == 1);

        // The icon coming later must not signal a second time.
        _tray.ShowNow();
        clock.Fire(TimeSpan.FromSeconds(2));
        Thread.Sleep(50);
        Assert.Equal(1, Volatile.Read(ref _winUCount));
    }

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);
    }

    private EaseOfAccessNotifier Notifier() =>
        new(_root, () => true, () => true, () => Interlocked.Increment(ref _winUCount) > 0, NullLogger.Instance, startedByEaseOfAccess: false);

    private static void WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        Assert.True(condition());
    }

    /// <summary>Holds the timers Task.Delay creates until the test fires those of one due time.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private readonly List<(TimeSpan Due, TimerCallback Callback, object? State)> _timers = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (_timers)
            {
                _timers.Add((dueTime, callback, state));
            }

            return new NoTimer();
        }

        public void Fire(TimeSpan due)
        {
            List<(TimeSpan Due, TimerCallback Callback, object? State)> matching;
            lock (_timers)
            {
                matching = _timers.FindAll(t => t.Due == due);
            }

            foreach (var timer in matching)
            {
                timer.Callback(timer.State);
            }
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTray : ITrayIcon
    {
        public event Action<TrayCommand>? Command
        {
            add { }
            remove { }
        }

        public event Action? Shown;

        public bool IsShown { get; set; }

        public void ShowNow()
        {
            IsShown = true;
            Shown?.Invoke();
        }

        public void Show(TrayMenu menu)
        {
        }

        public void Notify(string title, string message, TrayCommand? clicked = null)
        {
        }

        public void Dispose()
        {
        }
    }
}
