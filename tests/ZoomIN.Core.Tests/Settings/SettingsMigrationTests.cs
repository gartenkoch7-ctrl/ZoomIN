using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.Settings;

/// <summary>Schema 1 → 2 (M17 A1): once to the margin, with a hint; afterwards the user's choice stays.</summary>
public sealed class SettingsMigrationTests
{
    [Theory]
    [InlineData(true, MouseFollowMode.Center)]
    [InlineData(false, MouseFollowMode.Fixpoint)]
    [InlineData(null, MouseFollowMode.Center)]
    public void OldFile_SwitchesToTheMarginOnce_AndAsksForTheHint(bool? centerMouse, MouseFollowMode previous)
    {
        // An old file has no MouseFollow; whatever was read for it, the migration decides.
        var old = new AppSettings { SchemaVersion = 1, CenterMouse = centerMouse, MouseFollow = MouseFollowMode.Fixpoint, MouseMargin = 0.3 };

        var result = SettingsMigration.Migrate(old);

        Assert.NotNull(result.PreviousMouseFollow);
        Assert.Equal(previous, result.PreviousMouseFollow);
        Assert.Equal(MouseFollowMode.Margin, result.Settings.MouseFollow);
        Assert.Equal(0.3, result.Settings.MouseMargin);
        Assert.True(result.Settings.MouseFollowHintPending);
        Assert.Equal(AppSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);
        Assert.Null(result.Settings.CenterMouse);
    }

    [Fact]
    public void SecondStart_ChangesNothing()
    {
        var first = SettingsMigration.Migrate(new AppSettings { SchemaVersion = 1, CenterMouse = true }).Settings;
        var chosen = first with { MouseFollow = MouseFollowMode.Center, MouseFollowHintPending = false };

        var second = SettingsMigration.Migrate(chosen);

        Assert.Null(second.PreviousMouseFollow);
        Assert.Equal(chosen, second.Settings);
    }

    [Fact]
    public void NewInstallation_KeepsTheFactorySettings_WithoutHint()
    {
        var result = SettingsMigration.Migrate(new AppSettings());

        Assert.Null(result.PreviousMouseFollow);
        Assert.Equal(MouseFollowMode.Margin, result.Settings.MouseFollow);
        Assert.False(result.Settings.MouseFollowHintPending);
    }

    [Fact]
    public void NewFile_WithALeftoverCenterMouse_KeepsTheChoice_AndDropsTheOldField()
    {
        var current = new AppSettings { MouseFollow = MouseFollowMode.Fixpoint, CenterMouse = true };

        var result = SettingsMigration.Migrate(current);

        Assert.Null(result.PreviousMouseFollow);
        Assert.Equal(MouseFollowMode.Fixpoint, result.Settings.MouseFollow);
        Assert.Null(result.Settings.CenterMouse);
    }

    [Fact]
    public void OldFile_WithTheOverviewKeyGivenToAnotherAction_LeavesTheOverviewUnassigned()
    {
        var old = new AppSettings { SchemaVersion = 1, WindowFilterHotkey = HotkeyGesture.Overview };

        var migrated = SettingsMigration.Migrate(old).Settings;

        Assert.Equal(0, migrated.OverviewHotkey.VirtualKey);
        Assert.Equal(HotkeyGesture.Overview, migrated.WindowFilterHotkey);
        Assert.Equal(HotkeyGesture.Overview, SettingsMigration.Migrate(new AppSettings { SchemaVersion = 1 }).Settings.OverviewHotkey);
    }

    [Fact]
    public void Mapping_FallsBackForBrokenValues()
    {
        var options = TrackingMapping.Options(new AppSettings { MouseFollow = (MouseFollowMode)42, MouseMargin = double.NaN });

        Assert.Equal(TrackingOptions.Default.MouseFollow, options.MouseFollow);
        Assert.Equal(TrackingOptions.Default.MouseMargin, options.MouseMargin);
        Assert.Equal(0.5, TrackingMapping.Options(new AppSettings { MouseMargin = 3 }).MouseMargin);
    }
}
