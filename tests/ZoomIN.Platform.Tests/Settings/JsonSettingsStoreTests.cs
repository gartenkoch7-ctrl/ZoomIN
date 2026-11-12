using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Platform.Settings;

namespace ZoomIN.Platform.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ZoomIN.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Load_WithoutFile_ReturnsDefaults()
    {
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

        AssertEquivalent(new AppSettings(), store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllValues()
    {
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);
        var settings = new AppSettings
        {
            LastZoomLevel = 3.5,
            AnimationMilliseconds = 0,
            ToggleHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x51),
            WheelModifiers = HotkeyModifiers.Alt,
            PerMonitorZoom = false,
            PerMonitorFilter = false,
            MonitorFilters = new Dictionary<string, MonitorFilter> { [@"\\.\DISPLAY2"] = new(true, FilterId.BlackOnYellow) },
            MonitorZoomLevels = new Dictionary<string, double> { [@"\\.\DISPLAY1"] = 2.5, [@"\\.\DISPLAY2"] = 4 },
            WindowFilterHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x57),
            WindowFilter = WindowFilterOptions.Default
                .WithRule(WindowFilterRule.For(new WindowKind("winword.exe", "OpusApp"), WindowFilterChoice.None))
                .WithRule(WindowFilterRule.For(new WindowKind("excel.exe", "XLMAIN"), WindowFilterChoice.Filter, FilterId.Grayscale))
                with { Sensitivity = WindowFilterSensitivity.Custom, CustomPercent = 70, AutoFilter = FilterId.WhiteOnBlue },
            CustomSchemes =
            [
                new CustomScheme { Id = FilterId.Custom1, Name = "Zwei Farben", Kind = SchemeKind.TwoColors, Color1 = "#A0FFA0", Color2 = "#00104A" },
                new CustomScheme
                {
                    Id = FilterId.Custom2, Name = "Grün auf Dunkelblau", Kind = SchemeKind.Deficiency, Strength = 70,
                    Replacements = [new ColorReplacement { From = "#FF0000", To = "#FFFF00", Tolerance = 5 }],
                },
                new CustomScheme { Id = FilterId.Custom3, Name = "Getönt", Kind = SchemeKind.Tint, Hue = "#00E5FF" },
                new CustomScheme
                {
                    Id = FilterId.Custom4, Name = "Nur ersetzen", Kind = SchemeKind.ReplaceOnly,
                    Replacements = [new ColorReplacement { From = "#0000FF", To = "#FFFFFF", Tolerance = 20 }],
                },
            ],
            ColorFilter = FilterId.Custom4,
            TextSharpness = 0.5,
            TextBold = 1,
            CaretMarkerShape = CaretMarkerShape.Wedges,
            CaretMarkerColor = "#FFD600",
            CrosshairShow = ShowCondition.HoldKey,
            MarkerHoldKey = MarkerHoldKey.RightCtrl,
            MouseFollow = MouseFollowMode.Center,
            OverviewHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4F),
            IslandsHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4A),
            IslandEditHotkey = default,
            Islands = new IslandOptions
            {
                Visible = false,
                Items =
                [
                    new IslandEntry
                    {
                        MonitorId = @"\\.\DISPLAY1", SourceX = 1800, SourceY = 1040, SourceWidth = 120, SourceHeight = 40,
                        TargetX = 1560, TargetY = 960, Level = 3, Still = true,
                    },
                ],
            },
        };

        store.Save(settings);

        AssertEquivalent(settings, store.Load());
    }

    [Fact]
    public void Save_KeepsInheritedInputSettingsAtTheTopLevel()
    {
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

        store.Save(new AppSettings { MarkerHoldKey = MarkerHoldKey.LeftAlt });

        // InputSettings is a base record only for the file-size rule (M18); the file shape must not change.
        using var json = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal("LeftAlt", json.RootElement.GetProperty("MarkerHoldKey").GetString());
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("ToggleHotkey").ValueKind);
        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("WheelModifiers").ValueKind);
    }

    [Fact]
    public void Save_WritesReadableModifierNames()
    {
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

        store.Save(new AppSettings());

        // Names instead of numbers keep the file editable by hand; flag order is up to the serializer.
        Assert.Matches("\"Modifiers\": \"(Alt, Control|Control, Alt)\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Load_BrokenFile_ReturnsDefaults_AndKeepsBackup()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ this is not json");
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

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
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

        var settings = store.Load();

        Assert.Equal(3.0, settings.LastZoomLevel);
        Assert.True(settings.PerMonitorZoom);
        Assert.Equal(150, settings.AnimationMilliseconds);
        Assert.NotNull(settings.MonitorZoomLevels);
        Assert.Equal(HotkeyGesture.ToggleZoom, settings.ToggleHotkey);
    }

    [Fact]
    public void OldFile_WithoutWindowFilter_KeepsItsDefaults_AndComputedValuesStayOutOfTheFile()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "LastZoomLevel": 2 }""");
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

        var loaded = store.Load();
        store.Save(loaded);

        Assert.True(loaded.WindowFilter.Enabled);
        Assert.Equal(0, loaded.WindowFilterHotkey.VirtualKey);
        Assert.DoesNotContain("ThresholdPercent", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_NullLevels_BecomeEmpty()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "MonitorZoomLevels": null }""");
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

        Assert.Empty(store.Load().MonitorZoomLevels);
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

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
        Assert.Equal(expected.WindowFilter.Rules, actual.WindowFilter.Rules);
        Assert.Equal(expected.Islands.Items, actual.Islands.Items);
        Assert.Equal(expected.CustomSchemes.Count, actual.CustomSchemes.Count);
        for (var i = 0; i < expected.CustomSchemes.Count; i++)
        {
            Assert.Equal(expected.CustomSchemes[i].Replacements, actual.CustomSchemes[i].Replacements);
            Assert.Equal(expected.CustomSchemes[i] with { Replacements = [] }, actual.CustomSchemes[i] with { Replacements = [] });
        }

        Assert.Equal(
            expected with
            {
                MonitorZoomLevels = actual.MonitorZoomLevels,
                LensMonitorZoomLevels = actual.LensMonitorZoomLevels,
                DockedMonitorZoomLevels = actual.DockedMonitorZoomLevels,
                MonitorFilters = actual.MonitorFilters,
                CustomSchemes = actual.CustomSchemes,
                WindowFilter = expected.WindowFilter with { Rules = actual.WindowFilter.Rules },
                Islands = expected.Islands with { Items = actual.Islands.Items },
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
