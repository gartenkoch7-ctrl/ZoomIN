using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Tests.Settings;

/// <summary>The freeze shortcuts (G2) came without a schema change: their defaults must not clash with older actions.</summary>
public sealed class FreezeHotkeyClashTests
{
    [Fact]
    public void Defaults_StayWhenFree()
    {
        var settings = SettingsMigration.Migrate(new AppSettings()).Settings;

        Assert.Equal(HotkeyGesture.FreezeMonitor, settings.FreezeMonitorHotkey);
        Assert.Equal(HotkeyGesture.NextMonitor, settings.NextMonitorHotkey);
    }

    [Fact]
    public void AGestureAnOlderActionUses_LeavesTheNewShortcutUnassigned()
    {
        var old = new AppSettings { ToggleHotkey = HotkeyGesture.FreezeMonitor, OverviewHotkey = HotkeyGesture.NextMonitor };

        var settings = SettingsMigration.Migrate(old).Settings;

        Assert.Equal(default, settings.FreezeMonitorHotkey);
        Assert.Equal(default, settings.NextMonitorHotkey);
        Assert.Equal(HotkeyGesture.FreezeMonitor, settings.ToggleHotkey);
    }

    [Fact]
    public void FreezeOnTheNextMonitorDefault_UnassignsNext()
    {
        var settings = SettingsMigration.WithoutFreezeClashes(new AppSettings { FreezeMonitorHotkey = HotkeyGesture.NextMonitor });

        Assert.Equal(HotkeyGesture.NextMonitor, settings.FreezeMonitorHotkey);
        Assert.Equal(default, settings.NextMonitorHotkey);
    }

    [Fact]
    public void AGestureTheUserRecorded_IsKept_EvenIfUsedTwice()
    {
        var chosen = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x50); // Ctrl+Alt+P, also Profile
        var settings = SettingsMigration.WithoutFreezeClashes(new AppSettings { FreezeMonitorHotkey = chosen });

        Assert.Equal(chosen, settings.FreezeMonitorHotkey);
    }

    [Fact]
    public void Schema1Files_AreCheckedToo()
    {
        var old = new AppSettings { SchemaVersion = 1, LensHotkey = HotkeyGesture.NextMonitor };

        Assert.Equal(default, SettingsMigration.Migrate(old).Settings.NextMonitorHotkey);
    }
}
