using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>The one-time move to the autostart task (ADR-0030, Q6 and Q9) on private keys and a fake schtasks.</summary>
public sealed class AutostartMigrationTests : IDisposable
{
    private const string Sid = "S-1-5-21-1-2-3-1001";

    private readonly TestRegistry _user = new("Migration.User");
    private readonly FakeSchtasks _schtasks = new();

    private string RunKey => _user.RootPath + @"\Run";

    private RegistryAutostart Run => new(RunKey, "\"C:\\Program Files\\ZoomIN\\current\\ZoomIN.exe\"");

    private bool HasTask => _schtasks.Tasks.ContainsKey(AutostartTask.Name(Sid));

    [Fact]
    public void RunValue_IsReplacedByTheTask()
    {
        new RegistryAutostart(RunKey, "\"C:\\Program Files\\ZoomIN\\app-1.0.0\\ZoomIN.exe\"").SetEnabled(true);

        Assert.True(Migration(installed: true).Migrate());

        Assert.False(Run.Exists);
        Assert.True(HasTask);

        // Every later start finds nothing to do.
        Assert.False(Migration(installed: true).Migrate());
    }

    [Fact]
    public void ListEntryOfTheSpikeBuild_IsReplacedByTheTask()
    {
        SetList("nvda_nvda_v1,ZoomIN");

        Assert.True(Migration(installed: true).Migrate());

        Assert.Equal("nvda_nvda_v1", List());
        Assert.True(HasTask);
    }

    [Fact]
    public void TaskAndLeftoverRunValue_LeftoverIsRemoved()
    {
        Migration(installed: true).Migrate();
        Run.SetEnabled(true);

        Assert.True(Migration(installed: true).Migrate());

        Assert.False(Run.Exists);
        Assert.True(HasTask);
    }

    [Fact]
    public void WithoutRunValueOrListEntry_AutostartStaysOff()
    {
        Assert.False(Migration(installed: true).Migrate());

        Assert.False(HasTask);
    }

    [Fact]
    public void PortableZip_KeepsTheRunKey()
    {
        Run.SetEnabled(true);

        Assert.False(Migration(installed: false).Migrate());

        Assert.True(Run.Exists);
        Assert.False(HasTask);
    }

    [Fact]
    public void TaskCannotBeCreated_KeepsTheRunValue()
    {
        Run.SetEnabled(true);
        _schtasks.CreateExitCode = 1;

        Assert.False(Migration(installed: true).Migrate());

        Assert.True(Run.Exists);
    }

    public void Dispose() => _user.Dispose();

    private AutostartMigration Migration(bool installed)
    {
        var executable = installed ? @"C:\Program Files\ZoomIN\current\ZoomIN.exe" : @"D:\Tools\ZoomIN\ZoomIN.exe";
        var selector = new AutostartSelector(executable, _user.Root, RunKey, _schtasks, Sid, NullLogger.Instance);
        return new AutostartMigration(selector, NullLogger.Instance);
    }

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
}
