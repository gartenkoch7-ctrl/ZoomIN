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
    public void BothOnOneGesture_KeepsFreeze()
    {
        var settings = SettingsMigration.WithoutFreezeClashes(new AppSettings { NextMonitorHotkey = HotkeyGesture.FreezeMonitor });

        Assert.Equal(HotkeyGesture.FreezeMonitor, settings.FreezeMonitorHotkey);
        Assert.Equal(default, settings.NextMonitorHotkey);
    }

    [Fact]
    public void Schema1Files_AreCheckedToo()
    {
        var old = new AppSettings { SchemaVersion = 1, LensHotkey = HotkeyGesture.NextMonitor };

        Assert.Equal(default, SettingsMigration.Migrate(old).Settings.NextMonitorHotkey);
    }
}
