using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>The autostart task (ADR-0030, Q9) against a fake schtasks.</summary>
public sealed class TaskSchedulerAutostartTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";
    private const string Exe = @"C:\Program Files\ZoomIN\current\ZoomIN.exe";

    private readonly FakeSchtasks _schtasks = new();

    [Fact]
    public void On_CreatesTheTaskFromTheXml_AndRemovesTheFile()
    {
        var autostart = Autostart();

        autostart.SetEnabled(true);

        Assert.True(autostart.IsEnabled);
        Assert.Equal(AutostartTask.Xml(Sid, Exe), _schtasks.Tasks[AutostartTask.Name(Sid)]);
        Assert.False(File.Exists(_schtasks.LastXmlFile));
    }

    [Fact]
    public void Off_DeletesTheTask_AndSucceedsWithoutOne()
    {
        var autostart = Autostart();
        autostart.SetEnabled(true);

        Assert.True(autostart.TrySetEnabled(false));
        Assert.False(autostart.IsEnabled);

        Assert.True(autostart.TrySetEnabled(false));
    }

    [Fact]
    public void Off_WithAHangingQuery_StillDeletes()
    {
        var autostart = Autostart();
        autostart.SetEnabled(true);
        _schtasks.QueryExitCode = -1;

        Assert.True(autostart.TrySetEnabled(false));
        Assert.Contains("/Delete", _schtasks.Calls);
        Assert.Empty(_schtasks.Tasks);
    }

    [Fact]
    public void Off_DeleteFailsAndQueryHangs_IsReportedAsFailed()
    {
        var autostart = Autostart();
        autostart.SetEnabled(true);
        _schtasks.DeleteExitCode = 5;
        _schtasks.QueryExitCode = -1;

        Assert.False(autostart.TrySetEnabled(false));
    }

    [Fact]
    public void FailedCreate_IsNotThrown_AndReadsAsOff()
    {
        _schtasks.CreateExitCode = 5;
        var autostart = Autostart();

        autostart.SetEnabled(true);

        Assert.False(autostart.IsEnabled);
    }

    private TaskSchedulerAutostart Autostart() => new(Exe, Sid, _schtasks, NullLogger.Instance);
}
