using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>The one-time move from the Run key to the Ease of Access list (ADR-0030, Q6) on private keys.</summary>
public sealed class AutostartMigrationTests : IDisposable
{
    private readonly TestRegistry _user = new("Migration.User");
    private readonly TestRegistry _machine = new("Migration.Machine");

    private string RunKey => _user.RootPath + @"\Run";

    private RegistryAutostart Run => new(RunKey, "\"C:\\Program Files\\ZoomIN\\current\\ZoomIN.exe\"");

    [Fact]
    public void RunValue_IsReplacedByTheListEntry()
    {
        RegisterAt();
        SetList("nvda_nvda_v1");
        new RegistryAutostart(RunKey, "\"C:\\Program Files\\ZoomIN\\app-1.0.0\\ZoomIN.exe\"").SetEnabled(true);

        Assert.True(Migration(installed: true).Migrate());

        Assert.False(Run.Exists);
        Assert.Equal("nvda_nvda_v1,ZoomIN", List());

        // Every later start finds nothing to do.
        Assert.False(Migration(installed: true).Migrate());
    }

    [Fact]
    public void WithoutRunValue_AutostartStaysOff()
    {
        RegisterAt();

        Assert.False(Migration(installed: true).Migrate());

        Assert.Null(List());
    }

    [Fact]
    public void PortableZip_KeepsTheRunKey()
    {
        RegisterAt();
        Run.SetEnabled(true);

        Assert.False(Migration(installed: false).Migrate());

        Assert.True(Run.Exists);
        Assert.Null(List());
    }

    [Fact]
    public void WithoutAtEntry_WaitsForIt()
    {
        Run.SetEnabled(true);

        Assert.False(Migration(installed: true).Migrate());
        Assert.True(Run.Exists);

        RegisterAt();
        Assert.True(Migration(installed: true).Migrate());
        Assert.False(Run.Exists);
    }

    public void Dispose()
    {
        _user.Dispose();
        _machine.Dispose();
    }

    private AutostartMigration Migration(bool installed)
    {
        var executable = installed ? @"C:\Program Files\ZoomIN\current\ZoomIN.exe" : @"D:\Tools\ZoomIN\ZoomIN.exe";
        var selector = new AutostartSelector(executable, new SecureDesktopRegistration(_machine.Root), _user.Root, RunKey, NullLogger.Instance);
        return new AutostartMigration(selector, NullLogger.Instance);
    }

    private void RegisterAt() =>
        new SecureDesktopRegistration(_machine.Root).Apply(new InstallLayout(@"C:\Program Files\ZoomIN"), choice: true);

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
