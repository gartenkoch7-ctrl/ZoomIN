using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Watchdog.Crashes;

namespace ZoomIN.Watchdog.Tests;

/// <summary>
/// The restart rules of the watchdog (ADR-0007, ADR-0016) with a fake process and tray (M14 step 11):
/// regular end, crash with cleanup and restart, giving up after too many crashes.
/// </summary>
public sealed class WatchdogRunnerTests : IDisposable
{
    private const string AppPath = @"C:\Program Files\ZoomIN\current\ZoomIN.exe";

    private readonly string _root = Directory.CreateTempSubdirectory("ZoomIN-WatchdogRunnerTests-").FullName;
    private readonly FakeSystem _system = new();
    private readonly List<string> _cleaned = [];
    private readonly TestFolders _folders;
    private DateTimeOffset _now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    public WatchdogRunnerTests() => _folders = new TestFolders(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ProcessThatCannotBeWatched_EndsTheWatchdog()
    {
        _system.CanWatch = false;

        Assert.Equal(1, Runner().Run(42, AppPath));
        Assert.Empty(_system.Calls);
        Assert.Empty(_cleaned);
    }

    [Fact]
    public void RegularExit_NeitherCleansNorRestarts()
    {
        _system.ExitCode = 0;

        Assert.Equal(0, Runner().Run(42, AppPath));
        Assert.Empty(_system.Calls);
        Assert.Empty(_cleaned);
        Assert.False(File.Exists(_folders.CrashLog()));
    }

    [Fact]
    public void Crash_RunsEveryCleanupStep_AndRestarts()
    {
        Assert.Equal(0, Runner().Run(42, AppPath));

        // The first step fails; the second runs anyway.
        Assert.Equal(["failing", "cursor"], _cleaned);
        Assert.Equal(["restart " + AppPath], _system.Calls.Select(c => c.Split('|')[0]));
        Assert.DoesNotContain(SecureDesktopMode.Switch, _system.Calls[0], StringComparison.Ordinal);
        Assert.True(File.Exists(_folders.CrashLog()));
    }

    [Fact]
    public void FourthCrashWithinTheWindow_GivesUpWithANotice()
    {
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(0, Runner().Run(42, AppPath));
            _now = _now.AddMinutes(1);
        }

        _system.Calls.Clear();
        Assert.Equal(2, Runner().Run(42, AppPath));
        Assert.Equal(["notify ZoomIN wurde beendet"], _system.Calls);
    }

    [Fact]
    public void CrashesOutsideTheWindow_KeepRestarting()
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(0, Runner().Run(42, AppPath));
            _now = _now + RestartPolicy.Window + TimeSpan.FromSeconds(1);
        }

        Assert.Equal(5, _system.Calls.Count);
        Assert.All(_system.Calls, call => Assert.StartsWith("restart ", call, StringComparison.Ordinal));
    }

    [Fact]
    public void SecureInstance_WaitsForTheDesktopSwitch_AndRestartsWithItsSwitch()
    {
        Assert.Equal(0, Runner().Run(42, AppPath, secure: true));

        Assert.Equal("pause 500", _system.Calls[0]);
        Assert.Contains(SecureDesktopMode.Switch, _system.Calls[1], StringComparison.Ordinal);
    }

    [Fact]
    public void SecureInstance_GivesUpSilently_UntilWindowsRestarts()
    {
        for (var i = 0; i < 3; i++)
        {
            Runner().Run(42, AppPath, secure: true);
        }

        _system.Calls.Clear();
        Assert.Equal(2, Runner().Run(42, AppPath, secure: true));
        Assert.Equal(["pause 500"], _system.Calls);
        Assert.True(File.Exists(_folders.SecureGaveUp()));
    }

    private WatchdogRunner Runner() =>
        new(_folders, NullLogger.Instance, [new Step("failing", _cleaned, fail: true), new Step("cursor", _cleaned)], _system, new Clock(this));

    private sealed class FakeSystem : IWatchdogSystem
    {
        public List<string> Calls { get; } = [];

        public bool CanWatch { get; set; } = true;

        public int ExitCode { get; set; } = -1073741819; // access violation

        public bool TryWaitForExit(int processId, out int exitCode, out string reason)
        {
            exitCode = ExitCode;
            reason = CanWatch ? string.Empty : "gone";
            return CanWatch;
        }

        public void Restart(string appPath, string arguments) => Calls.Add($"restart {appPath}|{arguments}");

        public void Notify(string title, string message, TimeSpan duration) => Calls.Add("notify " + title);

        public void Pause(TimeSpan duration) => Calls.Add("pause " + duration.TotalMilliseconds);
    }

    private sealed class Step(string name, List<string> cleaned, bool fail = false) : ICleanupStep
    {
        public string Name => name;

        public void Run()
        {
            cleaned.Add(name);
            if (fail)
            {
                throw new InvalidOperationException("cleanup failed");
            }
        }
    }

    private sealed class Clock(WatchdogRunnerTests test) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => test._now;
    }

    private sealed record TestFolders(string Root) : IAppFolders
    {
        public string Local => Root;

        public string Roaming => Path.Combine(Root, "roaming");
    }
}
