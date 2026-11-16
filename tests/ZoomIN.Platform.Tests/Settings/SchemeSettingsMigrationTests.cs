using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Platform.Settings;

namespace ZoomIN.Platform.Tests.Settings;

/// <summary>
/// M18: the filter became a <see cref="FilterId"/> with places for own schemes. Settings written
/// before (filter names of the old enum, rules per window) must load without loss.
/// </summary>
public sealed class SchemeSettingsMigrationTests : IDisposable
{
    // Written by 1.1: filters by name, one as a number, no CustomSchemes.
    private const string OldSettings = """
        {
          "SchemaVersion": 1,
          "FilterEnabled": true,
          "ColorFilter": "YellowOnBlack",
          "MonitorFilters": {
            "\\\\.\\DISPLAY1": { "Enabled": true, "Filter": "InvertedSmart" },
            "\\\\.\\DISPLAY2": { "Enabled": false, "Filter": 6 }
          },
          "WindowFilter": {
            "Enabled": true,
            "AutoFilter": "InvertedKeepHue",
            "AutoFilterRevision": 1,
            "Rules": [
              { "Process": "winword.exe", "WindowClass": "OpusApp", "Choice": "Filter", "Filter": "GrayscaleInverted", "TitleBar": "Never" },
              { "Process": "excel.exe", "WindowClass": "XLMAIN", "Choice": "None", "Filter": "BlackOnYellow" }
            ]
          }
        }
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ZoomIN.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void OldFilterNames_LoadWithoutLoss_AndStayNamesWhenSaved()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, OldSettings);
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

        var loaded = store.Load();

        Assert.Equal(FilterId.YellowOnBlack, loaded.ColorFilter);
        Assert.Equal(new MonitorFilter(true, FilterId.InvertedSmart), loaded.MonitorFilters[@"\\.\DISPLAY1"]);
        Assert.Equal(new MonitorFilter(false, FilterId.WhiteOnBlue), loaded.MonitorFilters[@"\\.\DISPLAY2"]);
        Assert.Empty(loaded.CustomSchemes);

        var options = new WindowFilterModel(loaded.WindowFilter).Options;
        Assert.Equal(FilterId.InvertedKeepHue, options.AutoFilter);
        Assert.Equal([FilterId.GrayscaleInverted, FilterId.BlackOnYellow], options.Rules.Select(r => r.Filter));
        Assert.Equal(WindowTitleBarChoice.Never, options.Rules[0].TitleBar);

        var effects = EffectsMapping.Effects(loaded);
        Assert.Equal(FilterId.YellowOnBlack, effects.Filter.Filter);
        Assert.Equal(ColorFilters.Build(effects.Filter), effects.Matrix);

        store.Save(loaded);
        var text = File.ReadAllText(SettingsPath);
        Assert.Contains("\"ColorFilter\": \"YellowOnBlack\"", text, StringComparison.Ordinal);
        Assert.Contains("\"Filter\": \"WhiteOnBlue\"", text, StringComparison.Ordinal);
        Assert.Equal(FilterId.WhiteOnBlue, store.Load().MonitorFilters[@"\\.\DISPLAY2"].Filter);
    }

    [Fact]
    public void OwnSchemes_AndRulesNamingThem_RoundTrip()
    {
        var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, SettingsPath);
        var rule = WindowFilterRule.For(new WindowKind("msedge.exe", "Chrome_WidgetWin_1"), WindowFilterChoice.Filter, FilterId.Custom4);
        var settings = new Core.Settings.AppSettings
        {
            ColorFilter = FilterId.Custom2,
            WindowFilter = WindowFilterOptions.Default.WithRule(rule) with { AutoFilter = FilterId.Custom2 },
            CustomSchemes =
            [
                new CustomScheme { Id = FilterId.Custom2, Name = "Bernstein", Kind = SchemeKind.Tint, Hue = "#FFC107" },
                new CustomScheme { Id = FilterId.Custom4, Name = "Rot-Grün", Kind = SchemeKind.Deficiency, Strength = 70 },
            ],
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(FilterId.Custom2, loaded.ColorFilter);
        Assert.Equal(FilterId.Custom2, loaded.WindowFilter.AutoFilter);
        Assert.Equal(FilterId.Custom4, Assert.Single(loaded.WindowFilter.Rules).Filter);
        Assert.Equal(["Bernstein", "Rot-Grün"], loaded.CustomSchemes.Select(s => s.Name));
        Assert.Equal(SchemeKind.Deficiency, loaded.CustomSchemes[1].Kind);
        Assert.Equal(70, loaded.CustomSchemes[1].Strength);
    }

    [Fact]
    public void ProfileFiles_LeaveTheSharedSchemesOut_KeepTheChoice()
    {
        var store = new JsonProfileStore(NullLogger<JsonProfileStore>.Instance, _directory);
        var settings = new Core.Settings.AppSettings
        {
            ColorFilter = FilterId.Custom1,
            CustomSchemes = [new CustomScheme { Id = FilterId.Custom1, Name = "Bernstein" }],
        };

        store.Save("Lesen", settings);
        var loaded = store.Load("Lesen")!;

        Assert.Equal(FilterId.Custom1, loaded.ColorFilter);
        Assert.Empty(loaded.CustomSchemes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
