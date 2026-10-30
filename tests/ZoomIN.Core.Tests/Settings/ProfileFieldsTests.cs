using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Settings;

public sealed class ProfileFieldsTests
{
    [Fact]
    public void ApplyDisplay_TakesDisplaySettings_KeepsSharedSettings()
    {
        var current = new AppSettings
        {
            Language = AppLanguage.English,
            ToggleHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x51),
            CheckForUpdates = true,
            LastMode = ZoomMode.Fullscreen,
        };
        var profile = new AppSettings
        {
            Language = AppLanguage.German,
            LastMode = ZoomMode.Lens,
            ColorFilter = ColorFilter.YellowOnBlack,
            FilterEnabled = true,
            PanMilliseconds = 0,
        };

        var result = ProfileFields.ApplyDisplay(current, profile);

        Assert.Equal(ZoomMode.Lens, result.LastMode);
        Assert.Equal(ColorFilter.YellowOnBlack, result.ColorFilter);
        Assert.True(result.FilterEnabled);
        Assert.Equal(0, result.PanMilliseconds);
        Assert.Equal(AppLanguage.English, result.Language);
        Assert.Equal(current.ToggleHotkey, result.ToggleHotkey);
        Assert.True(result.CheckForUpdates);
    }

    [Theory]
    [InlineData("Büro", true)]
    [InlineData("Abends 2", true)]
    [InlineData("a/b", false)]
    [InlineData("  ", false)]
    [InlineData("..", false)]
    public void IsValidName_AllowsOnlySafeFileNames(string name, bool valid)
    {
        Assert.Equal(valid, ProfileFields.IsValidName(name));
    }

    [Fact]
    public void ZoomModel_Setters_RaiseChanged()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, true, 0.15);
        var raised = 0;
        model.Changed += () => raised++;

        model.Lens = new LensSize(0.5, 0.5);
        model.Dock = new DockLayout(DockEdge.Bottom, 0.3);
        model.AnimationSeconds = 0;
        model.PerMonitor = false;
        model.SetMode(ZoomMode.Docked);

        Assert.Equal(5, raised);
        Assert.Equal(ZoomMode.Docked, model.Mode);
    }

    [Fact]
    public void ZoomModel_ReplaceLevels_TakesOverAllLevels()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, true, 0.15);
        var other = new ZoomLevels();
        other.Set(ZoomMode.Lens, @"\\.\DISPLAY1", 6, perMonitor: true);

        model.ReplaceLevels(other);

        Assert.Equal(6, model.LastLevelFor(ZoomMode.Lens, @"\\.\DISPLAY1"));
    }
}
