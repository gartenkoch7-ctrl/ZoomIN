using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>Which autostart a ZoomIN uses (ADR-0030); private keys stand in for HKCU, HKLM and the Run key.</summary>
public sealed class AutostartSelectorTests : IDisposable
{
    private const string Installed = @"C:\Program Files\ZoomIN\current\ZoomIN.exe";

    private readonly TestRegistry _user = new("Selector.User");
    private readonly TestRegistry _machine = new("Selector.Machine");
    private readonly InstallLayout _layout = new(@"C:\Program Files\ZoomIN");

    private string RunKey => _user.RootPath + @"\Run";

    [Fact]
    public void Installed_UsesTheEaseOfAccessList_AndDropsAnOldRunValue()
    {
        RegisterAt();
        new RegistryAutostart(RunKey, "\"C:\\Old\\ZoomIN.exe\"").SetEnabled(true);

        var autostart = Selector(Installed);
        autostart.SetEnabled(true);

        Assert.Equal("ease_of_access", autostart.Method);
        Assert.True(autostart.IsEnabled);
        Assert.Equal("ZoomIN", List());
        Assert.Null(RunValue());
    }

    [Fact]
    public void Installed_Off_RemovesListEntryAndRunValue()
    {
        RegisterAt();
        SetList("nvda_nvda_v1,ZoomIN");
        new RegistryAutostart(RunKey, $"\"{Installed}\"").SetEnabled(true);

        Selector(Installed).SetEnabled(false);

        Assert.Equal("nvda_nvda_v1", List());
        Assert.Null(RunValue());
    }

    [Fact]
    public void Installed_ReadsTheList_SoWindowsSettingsWin()
    {
        RegisterAt();
        var autostart = Selector(Installed);
        autostart.SetEnabled(true);

        // The user switches ZoomIN off in Windows' Settings › Accessibility.
        SetList(string.Empty);

        Assert.False(autostart.IsEnabled);
    }

    [Fact]
    public void Installed_WithoutAtEntry_KeepsTheRunKey()
    {
        // An update applied by an older version has not written ATs\ZoomIN yet.
        SetList("ZoomIN");

        var autostart = Selector(Installed);
        autostart.SetEnabled(true);

        Assert.Equal("run", autostart.Method);
        Assert.True(autostart.IsInstalled);
        Assert.Equal($"\"{Installed}\"", RunValue());
        Assert.Equal(string.Empty, List());
    }

    [Fact]
    public void AtEntryWrittenWhileRunning_SwitchesToTheList()
    {
        var autostart = Selector(Installed);
        Assert.False(autostart.UsesEaseOfAccess);

        // The user turns the sign-in screen switch on: the elevated step writes ATs\ZoomIN.
        RegisterAt();
        autostart.SetEnabled(true);

        Assert.Equal("ease_of_access", autostart.Method);
        Assert.Equal("ZoomIN", List());
        Assert.Null(RunValue());
    }

    [Fact]
    public void LeftoverRunValue_CountsAsOn_UntilSwitchedOff()
    {
        // Migration waited for the AT entry; meanwhile the Run value still starts ZoomIN.
        new RegistryAutostart(RunKey, $"\"{Installed}\"").SetEnabled(true);
        RegisterAt();
        var autostart = Selector(Installed);

        Assert.True(autostart.IsEnabled);

        autostart.SetEnabled(false);
        Assert.False(autostart.IsEnabled);
        Assert.Null(RunValue());
    }

    [Fact]
    public void VersionFolder_WritesTheCurrentLink()
    {
        // An installed ZoomIN may run from app-1.2.0; an update removes that folder later.
        Selector(@"C:\Program Files\ZoomIN\app-1.2.0\ZoomIN.exe").SetEnabled(true);

        Assert.Equal($"\"{Installed}\"", RunValue());
    }

    [Fact]
    public void NotInstalled_UsesTheRunKey_AndLeavesTheListAlone()
    {
        RegisterAt();
        SetList("ZoomIN");

        var autostart = Selector(@"D:\Tools\ZoomIN\ZoomIN.exe");
        autostart.SetEnabled(true);
        Assert.Equal("run", autostart.Method);
        Assert.False(autostart.IsInstalled);
        Assert.Equal("\"D:\\Tools\\ZoomIN\\ZoomIN.exe\"", RunValue());

        autostart.SetEnabled(false);
        Assert.Null(RunValue());
        Assert.Equal("ZoomIN", List());
    }

    public void Dispose()
    {
        _user.Dispose();
        _machine.Dispose();
    }

    private AutostartSelector Selector(string executable) =>
        new(executable, new SecureDesktopRegistration(_machine.Root), _user.Root, RunKey, NullLogger.Instance);

    private void RegisterAt() => new SecureDesktopRegistration(_machine.Root).Apply(_layout, choice: false);

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
