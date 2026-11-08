using ZoomIN.Core.Folders;
using ZoomIN.Watchdog;
using ZoomIN.Watchdog.Crashes;

namespace ZoomIN.Watchdog.Tests.Crashes;

/// <summary>
/// What the watchdog keeps on disk (ADR-0007, ADR-0016), in a test folder through IAppFolders
/// (M14 step 7) – and its command line.
/// </summary>
public sealed class WatchdogFilesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ZoomIN-WatchdogTests-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void GiveUp_IsActiveAfterMarking_UntilWindowsRestarts()
    {
        var folders = new TestFolders(_folder);
        var giveUp = new SecureDesktopGiveUp(folders, new FixedClock(DateTimeOffset.Now));

        Assert.False(giveUp.IsActive());

        giveUp.Mark();

        Assert.True(giveUp.IsActive());
        Assert.True(File.Exists(folders.SecureGaveUp()));
    }

    [Fact]
    public void GiveUp_MarkedBeforeTheLastBoot_IsOver()
    {
        var folders = new TestFolders(_folder);
        var longAgo = DateTimeOffset.Now - TimeSpan.FromMilliseconds(Environment.TickCount64) - TimeSpan.FromHours(1);
        new SecureDesktopGiveUp(folders, new FixedClock(longAgo)).Mark();

        Assert.False(new SecureDesktopGiveUp(folders).IsActive());
    }

    [Fact]
    public void CrashHistory_KeepsTheLatestTwenty()
    {
        var history = new CrashHistory(new TestFolders(_folder).CrashLog());
        var start = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        IReadOnlyList<DateTimeOffset> times = [];

        for (var i = 0; i < 25; i++)
        {
            times = history.Record(start.AddMinutes(i));
        }

        // The file keeps 20 lines; a record returns them plus the new crash.
        Assert.Equal(21, times.Count);
        var kept = new CrashHistory(new TestFolders(_folder).CrashLog()).Record(start.AddMinutes(30));
        Assert.Equal(21, kept.Count);
        Assert.Equal(start.AddMinutes(5), kept[0]);
        Assert.Equal(start.AddMinutes(30), kept[^1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Arguments_RoundTrip(bool secure)
    {
        var args = WatchdogArguments.Format(4321, secure).Split(' ');

        Assert.True(WatchdogArguments.TryParse(args, out var processId, out var parsedSecure));
        Assert.Equal(4321, processId);
        Assert.Equal(secure, parsedSecure);
    }

    private sealed record TestFolders(string Root) : IAppFolders
    {
        public string Local => Root;

        public string Roaming => Path.Combine(Root, "roaming");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}
