using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>
/// Which autostart a ZoomIN uses (ADR-0030, Q9): a private key stands in for HKCU and the Run key,
/// a fake for schtasks.
/// </summary>
public sealed class AutostartSelectorTests : IDisposable
{
    private const string Installed = @"C:\Program Files\ZoomIN\current\ZoomIN.exe";
    private const string Sid = "S-1-5-21-1-2-3-1001";

    private readonly TestRegistry _user = new("Selector.User");
    private readonly FakeSchtasks _schtasks = new();

    private string RunKey => _user.RootPath + @"\Run";

    private bool HasTask => _schtasks.Tasks.ContainsKey(AutostartTask.Name(Sid));

    [Fact]
    public void Installed_UsesTheTask_AndDropsRunValueAndListEntry()
    {
        new RegistryAutostart(RunKey, "\"C:\\Old\\ZoomIN.exe\"").SetEnabled(true);
        SetList("nvda_nvda_v1,ZoomIN");

        var autostart = Selector(Installed);
        autostart.SetEnabled(true);

        Assert.Equal("task", autostart.Method);
        Assert.True(autostart.IsEnabled);
        Assert.True(HasTask);
        Assert.Null(RunValue());
        Assert.Equal("nvda_nvda_v1", List());
    }

    [Fact]
    public void Installed_Off_RemovesTaskRunValueAndListEntry()
    {
        var autostart = Selector(Installed);
        autostart.SetEnabled(true);
        new RegistryAutostart(RunKey, $"\"{Installed}\"").SetEnabled(true);
        SetList("ZoomIN");

        autostart.SetEnabled(false);

        Assert.False(autostart.IsEnabled);
        Assert.False(HasTask);
        Assert.Null(RunValue());
        Assert.Equal(string.Empty, List());
    }

    [Fact]
    public void TaskDeletedByTheUser_ReadsAsOff()
    {
        var autostart = Selector(Installed);
        autostart.SetEnabled(true);

        // The user deletes the task in the Task Scheduler.
        _schtasks.Run(["/Delete", "/TN", AutostartTask.Name(Sid), "/F"]);

        Assert.False(autostart.IsEnabled);
    }

    [Fact]
    public void TaskCannotBeCreated_KeepsTheOldAutostart()
    {
        new RegistryAutostart(RunKey, $"\"{Installed}\"").SetEnabled(true);
        _schtasks.CreateExitCode = 1;

        var autostart = Selector(Installed);
        autostart.SetEnabled(true);

        Assert.False(HasTask);
        Assert.Equal($"\"{Installed}\"", RunValue());
        Assert.True(autostart.IsEnabled);
    }

    [Fact]
    public void TasksBlocked_FreshInstall_FallsBackToTheRunKey()
    {
        _schtasks.CreateExitCode = 1;

        var autostart = Selector(Installed);

        Assert.False(autostart.TrySetEnabled(true));
        Assert.Equal($"\"{Installed}\"", RunValue());
        Assert.True(autostart.IsEnabled);
    }

    [Fact]
    public void TasksBlocked_ListEntry_IsReplacedByTheRunKey()
    {
        SetList("nvda_nvda_v1,ZoomIN");
        _schtasks.CreateExitCode = 1;

        Selector(Installed).SetEnabled(true);

        Assert.Equal($"\"{Installed}\"", RunValue());
        Assert.Equal("nvda_nvda_v1", List());
    }

    [Fact]
    public void CreateTimesOutAfterRegistering_CountsAsDone()
    {
        _schtasks.CreateExitCode = SchtasksProcess.TimedOut;
        _schtasks.RegisterDespiteFailure = true;
        new RegistryAutostart(RunKey, $"\"{Installed}\"").SetEnabled(true);

        Assert.True(Selector(Installed).TrySetEnabled(true));
        Assert.Null(RunValue());
    }

    [Fact]
    public void CreateAndQueryTimeOut_WritesNoRunValue_KeepsAnOldOne()
    {
        _schtasks.CreateExitCode = SchtasksProcess.TimedOut;
        _schtasks.QueryExitCode = SchtasksProcess.TimedOut;

        Assert.False(Selector(Installed).TrySetEnabled(true));
        Assert.Null(RunValue());

        new RegistryAutostart(RunKey, $"\"{Installed}\"").SetEnabled(true);
        Selector(Installed).TrySetEnabled(true);
        Assert.Equal($"\"{Installed}\"", RunValue());
    }

    [Fact]
    public void DeleteFails_StaysOn_AndReportsIt()
    {
        var autostart = Selector(Installed);
        autostart.SetEnabled(true);
        _schtasks.DeleteExitCode = 5;

        Assert.False(autostart.TrySetEnabled(false));
        Assert.True(autostart.IsEnabled);
    }

    [Fact]
    public void Leftovers_CountAsOn()
    {
        SetList("ZoomIN");

        Assert.True(Selector(Installed).IsEnabled);
    }

    [Fact]
    public void VersionFolder_WritesTheCurrentLink()
    {
        // An installed ZoomIN may run from app-1.2.0; an update removes that folder later.
        Selector(@"C:\Program Files\ZoomIN\app-1.2.0\ZoomIN.exe").SetEnabled(true);

        Assert.Contains($"\"{Installed}\"", _schtasks.Tasks[AutostartTask.Name(Sid)], StringComparison.Ordinal);
    }

    [Fact]
    public void NotInstalled_UsesTheRunKey_AndTouchesNeitherTaskNorList()
    {
        SetList("ZoomIN");

        var autostart = Selector(@"D:\Tools\ZoomIN\ZoomIN.exe");
        autostart.SetEnabled(true);
        Assert.Equal("run", autostart.Method);
        Assert.False(autostart.IsInstalled);
        Assert.Equal("\"D:\\Tools\\ZoomIN\\ZoomIN.exe\"", RunValue());

        autostart.SetEnabled(false);
        Assert.Null(RunValue());
        Assert.Equal("ZoomIN", List());
        Assert.Empty(_schtasks.Calls);
    }

    public void Dispose() => _user.Dispose();

    private AutostartSelector Selector(string executable) =>
        new(executable, _user.Root, RunKey, _schtasks, Sid, NullLogger.Instance);

    private void SetList(string list)
    {
        using var key = _user.Root.CreateSubKey(AtStartList.KeyPath);
        key.SetValue("Configuration", list);
    }

    private string? List()
    {
        using var key = _user.Root.OpenSubKey(AtStartList.KeyPath);
        return key?.GetValue("Configuration") as string;
    }

    private string? RunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("ZoomIN") as string;
    }
}
