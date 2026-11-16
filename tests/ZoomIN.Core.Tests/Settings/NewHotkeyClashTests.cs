using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Tests.Settings;

/// <summary>The M19 shortcuts (islands B1, freeze G2) came without a schema change: their defaults must not clash with older actions.</summary>
public sealed class NewHotkeyClashTests
{
    [Fact]
    public void Defaults_StayWhenFree()
    {
        var settings = SettingsMigration.Migrate(new AppSettings()).Settings;

        Assert.Equal(HotkeyGesture.FreezeMonitor, settings.FreezeMonitorHotkey);
        Assert.Equal(HotkeyGesture.NextMonitor, settings.NextMonitorHotkey);
        Assert.Equal(HotkeyGesture.ToggleIslands, settings.IslandsHotkey);
        Assert.Equal(HotkeyGesture.EditIslands, settings.IslandEditHotkey);
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
        var settings = SettingsMigration.WithoutNewHotkeyClashes(new AppSettings { FreezeMonitorHotkey = HotkeyGesture.NextMonitor });

        Assert.Equal(HotkeyGesture.NextMonitor, settings.FreezeMonitorHotkey);
        Assert.Equal(default, settings.NextMonitorHotkey);
    }

    [Fact]
    public void AGestureTheUserRecorded_IsKept_EvenIfUsedTwice()
    {
        var chosen = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x50); // Ctrl+Alt+P, also Profile
        var settings = SettingsMigration.WithoutNewHotkeyClashes(new AppSettings { FreezeMonitorHotkey = chosen });

        Assert.Equal(chosen, settings.FreezeMonitorHotkey);
    }

    [Fact]
    public void IslandDefaults_GiveWayToOlderActions()
    {
        var old = new AppSettings { TrackingHotkey = HotkeyGesture.ToggleIslands };

        var settings = SettingsMigration.Migrate(old).Settings;

        Assert.Equal(default, settings.IslandsHotkey);
        Assert.Equal(HotkeyGesture.EditIslands, settings.IslandEditHotkey);
        Assert.Equal(HotkeyGesture.ToggleIslands, settings.TrackingHotkey);
    }

    [Fact]
    public void ARecordedM19Gesture_WinsOverTheFactoryOneOfAnotherM19Shortcut()
    {
        var islandsOnH = SettingsMigration.WithoutNewHotkeyClashes(new AppSettings { IslandsHotkey = HotkeyGesture.FreezeMonitor });
        Assert.Equal(HotkeyGesture.FreezeMonitor, islandsOnH.IslandsHotkey);
        Assert.Equal(default, islandsOnH.FreezeMonitorHotkey);

        var freezeOnI = SettingsMigration.WithoutNewHotkeyClashes(new AppSettings { FreezeMonitorHotkey = HotkeyGesture.ToggleIslands });
        Assert.Equal(default, freezeOnI.IslandsHotkey);
        Assert.Equal(HotkeyGesture.ToggleIslands, freezeOnI.FreezeMonitorHotkey);
    }

    // The clash check relies on this: a factory gesture is never "unassigned" and never another factory or fixed gesture.
    [Fact]
    public void FactoryGestures_AreAssigned_AndDistinct()
    {
        var s = new AppSettings();
        HotkeyGesture[] factory =
        [
            s.ToggleHotkey, s.ZoomInHotkey, s.ZoomOutHotkey, s.FullscreenHotkey, s.LensHotkey, s.DockedHotkey, s.FilterHotkey,
            s.NextFilterHotkey, s.LocateHotkey, s.CrosshairHotkey, s.TrackingHotkey, s.ProfileHotkey, s.OverviewHotkey,
            s.IslandsHotkey, s.IslandEditHotkey, s.FreezeMonitorHotkey, s.NextMonitorHotkey,
            HotkeyGesture.ExitSecureDesktop, HotkeyGesture.SecureDesktopSnapshot, HotkeyGesture.LensWider, HotkeyGesture.LensNarrower, HotkeyGesture.LensTaller, HotkeyGesture.LensShorter,
        ];

        Assert.All(factory, gesture => Assert.NotEqual(default(HotkeyGesture), gesture));
        Assert.Equal(factory.Length, factory.Distinct().Count());
    }

    [Fact]
    public void Schema1Files_AreCheckedToo()
    {
        var old = new AppSettings { SchemaVersion = 1, LensHotkey = HotkeyGesture.NextMonitor };

        Assert.Equal(default, SettingsMigration.Migrate(old).Settings.NextMonitorHotkey);
    }
}
