using ZoomIN.App.SecureDesktop.Checks;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;

namespace ZoomIN.App.Tests.SecureDesktop;

/// <summary>Strg+Alt+Umschalt+D on the secure desktop (M10, Q8): a debug snapshot into the own debug folder.</summary>
public sealed class SecureSnapshotShortcutTests
{
    private readonly DebugSnapshots _snapshots = new();
    private readonly CapturingLog<SecureSnapshotShortcut> _log = new();

    [Fact]
    public async Task ASnapshot_GoesIntoTheDebugFolder_AndIsLogged()
    {
        string? folder = null;
        var time = new AnsweringTime(() =>
        {
            folder = _snapshots.Pending!.Folder;
            _snapshots.Report(new DebugSnapshotResult(_snapshots.Pending.Generation, @"\\.\DISPLAY1", true, "saved"));
        });

        await new SecureSnapshotShortcut(_snapshots, new Folders(), time, _log).TakeAsync();

        Assert.Equal(Path.Combine(@"C:\System\ZoomIN", "debug", "secure-snapshot-"), folder![..^15]);
        Assert.Null(_snapshots.Pending);
        Assert.Contains("1 monitor(s) saved in secure-snapshot-", _log.Text(1164), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoAnswer_IsAWarning_AndTheRequestIsWithdrawn()
    {
        await new SecureSnapshotShortcut(_snapshots, new Folders(), new AnsweringTime(() => { }), _log).TakeAsync();

        Assert.Null(_snapshots.Pending);
        Assert.Contains(_log.Lines, line => line.Id == 1165);
    }

    private sealed class Folders : IAppFolders
    {
        public string Local => @"C:\System\ZoomIN";

        public string Roaming => @"C:\System\Roaming\ZoomIN";
    }

    /// <summary>Every wait ends at once, after a renderer had the chance to answer.</summary>
    private sealed class AnsweringTime(Action answer) : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            answer();

            // Not inline: the caller has not stored its timer yet.
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoTimer();
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
}
