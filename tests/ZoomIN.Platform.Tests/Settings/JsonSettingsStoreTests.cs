using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Settings;
using ZoomIN.Platform.Settings;

namespace ZoomIN.Platform.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ZoomIN.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Load_WithoutFile_ReturnsDefaults()
    {
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        AssertEquivalent(new AppSettings(), store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllValues()
    {
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);
        var settings = new AppSettings
        {
            LastZoomLevel = 3.5,
            AnimationMilliseconds = 0,
            ToggleHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x51),
            WheelModifiers = HotkeyModifiers.Alt,
            PerMonitorZoom = false,
            PerMonitorFilter = false,
            MonitorFilters = new Dictionary<string, MonitorFilter> { [@"\\.\DISPLAY2"] = new(true, ColorFilter.BlackOnYellow) },
            MonitorZoomLevels = new Dictionary<string, double> { [@"\\.\DISPLAY1"] = 2.5, [@"\\.\DISPLAY2"] = 4 },
        };

        store.Save(settings);

        AssertEquivalent(settings, store.Load());
    }

    [Fact]
    public void Save_WritesReadableModifierNames()
    {
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        store.Save(new AppSettings());

        // Names instead of numbers keep the file editable by hand; flag order is up to the serializer.
        Assert.Matches("\"Modifiers\": \"(Alt, Control|Control, Alt)\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Load_BrokenFile_ReturnsDefaults_AndKeepsBackup()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ this is not json");
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        var settings = store.Load();

        AssertEquivalent(new AppSettings(), settings);
        Assert.True(File.Exists(SettingsPath + ".broken"));
    }

    [Fact]
    public void Load_OlderFileWithoutNewProperties_KeepsTheirDefaults()
    {
        // Settings file as written by M2: no PerMonitorZoom, no MonitorZoomLevels.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "SchemaVersion": 1, "LastZoomLevel": 3.0, "WheelModifiers": "Control" }""");
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        var settings = store.Load();

        Assert.Equal(3.0, settings.LastZoomLevel);
        Assert.True(settings.PerMonitorZoom);
        Assert.Equal(150, settings.AnimationMilliseconds);
        Assert.NotNull(settings.MonitorZoomLevels);
        Assert.Equal(HotkeyGesture.ToggleZoom, settings.ToggleHotkey);
    }

    [Fact]
    public void Load_NullLevels_BecomeEmpty()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "MonitorZoomLevels": null }""");
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        Assert.Empty(store.Load().MonitorZoomLevels);
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        store.Save(new AppSettings());

        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    // Records compare dictionaries by reference; compare the levels by content instead.
    private static void AssertEquivalent(AppSettings expected, AppSettings actual)
    {
        Assert.Equal(expected.MonitorZoomLevels, actual.MonitorZoomLevels);
        Assert.Equal(expected.LensMonitorZoomLevels, actual.LensMonitorZoomLevels);
        Assert.Equal(expected.DockedMonitorZoomLevels, actual.DockedMonitorZoomLevels);
        Assert.Equal(expected.MonitorFilters, actual.MonitorFilters);
        Assert.Equal(
            expected with
            {
                MonitorZoomLevels = actual.MonitorZoomLevels,
                LensMonitorZoomLevels = actual.LensMonitorZoomLevels,
                DockedMonitorZoomLevels = actual.DockedMonitorZoomLevels,
                MonitorFilters = actual.MonitorFilters,
            },
            actual);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
