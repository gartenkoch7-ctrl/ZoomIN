using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.SecureDesktop;

public sealed class SecureSettingsSanitizerTests
{
    [Fact]
    public void ValidSettings_StayUnchanged()
    {
        var settings = new AppSettings
        {
            LastMode = ZoomMode.Lens,
            ZoomOn = true,
            Contrast = 1.5,
            CursorColor = "#FF00FF",
            MonitorZoomLevels = new Dictionary<string, double> { [@"\\.\DISPLAY1"] = 4 },
            MonitorFilters = new Dictionary<string, MonitorFilter> { [@"\\.\DISPLAY2"] = new(true, ColorFilter.YellowOnBlack) },
        };

        var result = SecureSettingsSanitizer.Sanitize(settings);

        Assert.Equal(ZoomMode.Lens, result.LastMode);
        Assert.True(result.ZoomOn);
        Assert.Equal(1.5, result.Contrast);
        Assert.Equal("#FF00FF", result.CursorColor);
        Assert.Equal(4, result.MonitorZoomLevels[@"\\.\DISPLAY1"]);
        Assert.Equal(ColorFilter.YellowOnBlack, result.MonitorFilters[@"\\.\DISPLAY2"].Filter);
    }

    [Fact]
    public void FilterPerWindow_IsOff_OnTheSecureDesktop()
    {
        var result = SecureSettingsSanitizer.Sanitize(new AppSettings
        {
            WindowFilter = WindowFilterOptions.Default.WithRule(
                WindowFilterRule.For(new WindowKind("logonui.exe", "x"), WindowFilterChoice.Filter)),
            WindowFilterHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x57),
        });

        Assert.False(result.WindowFilter.Enabled);
        Assert.Empty(result.WindowFilter.Rules);
        Assert.Equal(0, result.WindowFilterHotkey.VirtualKey);
    }

    [Fact]
    public void NumbersOutOfRange_AreClamped_NaN_BecomesDefault()
    {
        var result = SecureSettingsSanitizer.Sanitize(new AppSettings
        {
            LastZoomLevel = 1000,
            Contrast = double.NaN,
            Brightness = -7,
            LensWidth = double.PositiveInfinity,
            PanMilliseconds = int.MaxValue,
            AnimationMilliseconds = -5,
        });

        Assert.Equal(16, result.LastZoomLevel);
        Assert.Equal(1.0, result.Contrast);
        Assert.Equal(-0.5, result.Brightness);
        Assert.Equal(LensSize.Default.WidthFraction, result.LensWidth);
        Assert.Equal(1000, result.PanMilliseconds);
        Assert.Equal(0, result.AnimationMilliseconds);
    }

    [Fact]
    public void UnknownEnumValues_BecomeDefaults()
    {
        var result = SecureSettingsSanitizer.Sanitize(new AppSettings
        {
            LastMode = (ZoomMode)99,
            ColorFilter = (ColorFilter)99,
            LocatorTriggers = (LocatorTriggers)0xFF,
            CaretMarkerShape = (CaretMarkerShape)99,
            CaretMarkerThickness = (CaretMarkerThickness)99,
            CaretMarkerColor = new string('c', 500),
        });

        Assert.Equal(new AppSettings().CaretMarkerShape, result.CaretMarkerShape);
        Assert.Equal(new AppSettings().CaretMarkerThickness, result.CaretMarkerThickness);
        Assert.Equal(new AppSettings().CaretMarkerColor, result.CaretMarkerColor);
        Assert.Equal(ZoomMode.Fullscreen, result.LastMode);
        Assert.Equal(new AppSettings().ColorFilter, result.ColorFilter);
        Assert.Equal(LocatorTriggers.Hotkey | LocatorTriggers.Shake | LocatorTriggers.AfterIdle | LocatorTriggers.CtrlKey, result.LocatorTriggers);
    }

    [Fact]
    public void OversizedAndBrokenEntries_AreDropped()
    {
        var levels = Enumerable.Range(0, 100).ToDictionary(i => $"M{i}", _ => 2.0);
        levels[new string('x', 500)] = 2;
        var filters = new Dictionary<string, MonitorFilter> { ["A"] = null!, ["B"] = new(true, (ColorFilter)42) };

        var result = SecureSettingsSanitizer.Sanitize(new AppSettings
        {
            MonitorZoomLevels = levels,
            LensMonitorZoomLevels = null!,
            MonitorFilters = filters,
            ActiveProfile = new string('p', 500),
            CursorColor = null!,
        });

        Assert.Equal(SecureSettingsSanitizer.MaxMonitorEntries, result.MonitorZoomLevels.Count);
        Assert.Empty(result.LensMonitorZoomLevels);
        Assert.Empty(result.MonitorFilters);
        Assert.Equal(new AppSettings().ActiveProfile, result.ActiveProfile);
        Assert.Equal(new AppSettings().CursorColor, result.CursorColor);
    }

    [Fact]
    public void HotkeysThatWouldBlockTyping_FallBackToDefaults()
    {
        var result = SecureSettingsSanitizer.Sanitize(new AppSettings
        {
            ToggleHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x51), // AltGr+Q = @
            ZoomInHotkey = new HotkeyGesture(HotkeyModifiers.Shift, 0x41), // Shift+A types "A"
            ZoomOutHotkey = new HotkeyGesture((HotkeyModifiers)0x40 | HotkeyModifiers.Control, 0x41),
            LensHotkey = new HotkeyGesture(HotkeyModifiers.Control, 0x11), // Ctrl as main key
            WheelModifiers = HotkeyModifiers.Shift,
        });

        Assert.Equal(HotkeyGesture.ToggleZoom, result.ToggleHotkey);
        Assert.Equal(HotkeyGesture.ZoomIn, result.ZoomInHotkey);
        Assert.Equal(HotkeyGesture.ZoomOut, result.ZoomOutHotkey);
        Assert.Equal(HotkeyGesture.Lens, result.LensHotkey);
        Assert.Equal(HotkeyModifiers.Control, result.WheelModifiers);
    }

    [Fact]
    public void ExitShortcut_DoesNotBlockAnAltGrCharacter() =>
        Assert.False(HotkeyRules.BlocksAltGrCharacter(HotkeyGesture.ExitSecureDesktop));
}
