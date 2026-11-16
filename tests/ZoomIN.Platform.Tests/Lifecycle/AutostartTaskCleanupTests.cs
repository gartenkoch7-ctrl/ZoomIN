using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>Uninstall removes every account's autostart task (Q9); a temp folder stands in for the task store.</summary>
public sealed class AutostartTaskCleanupTests : IDisposable
{
    private readonly string _store = Directory.CreateTempSubdirectory("ZoomIN-TaskStore-").FullName;
    private readonly FakeSchtasks _schtasks = new();

    [Fact]
    public void DeletesTheTasksOfAllAccounts_AndNothingElse()
    {
        var mine = Register("S-1-5-21-1-2-3-1001");
        var other = Register("S-1-5-21-1-2-3-1002");
        File.WriteAllText(Path.Combine(_store, "OneDrive Standalone Update Task"), "x");
        Directory.CreateDirectory(Path.Combine(_store, "Microsoft"));

        Assert.Equal(2, Cleanup().RemoveAll());

        Assert.Empty(_schtasks.Tasks);
        Assert.Equal(["/Delete", "/Delete"], _schtasks.Calls);
        Assert.DoesNotContain(mine, _schtasks.Tasks.Keys);
        Assert.DoesNotContain(other, _schtasks.Tasks.Keys);
    }

    [Fact]
    public void DeleteFails_CountsOnlyTheDeleted()
    {
        Register("S-1-5-21-1-2-3-1001");
        _schtasks.DeleteExitCode = 5;

        Assert.Equal(0, Cleanup().RemoveAll());
    }

    [Fact]
    public void UnreadableStore_RemovesNothing_WithoutThrowing()
    {
        var cleanup = new AutostartTaskCleanup(_schtasks, Path.Combine(_store, "missing"), NullLogger.Instance);

        Assert.Equal(0, cleanup.RemoveAll());
        Assert.Empty(_schtasks.Calls);
    }

    public void Dispose() => Directory.Delete(_store, recursive: true);

    private AutostartTaskCleanup Cleanup() => new(_schtasks, _store, NullLogger.Instance);

    // Like the Task Scheduler: a file in the store and a registered task of the same name.
    private string Register(string sid)
    {
        var name = AutostartTask.Name(sid);
        File.WriteAllText(Path.Combine(_store, name), "task");
        var xml = Path.Combine(_store, name + ".xml.tmp");
        File.WriteAllText(xml, "x", System.Text.Encoding.Unicode);
        _schtasks.Run(["/Create", "/TN", name, "/XML", xml, "/F"]);
        File.Delete(xml);
        _schtasks.Calls.Clear();
        return name;
    }
}
