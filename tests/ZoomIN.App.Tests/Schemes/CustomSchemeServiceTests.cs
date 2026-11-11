using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Localization;
using ZoomIN.App.Schemes;
using ZoomIN.App.Settings;
using ZoomIN.App.ViewModels;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Tests.Schemes;

/// <summary>Own colour schemes (M18): create, the limit of four, delete with fall-back, profiles.</summary>
public sealed class CustomSchemeServiceTests
{
    private const string Display1 = @"\\.\DISPLAY1";
    private static readonly WindowKind s_word = new("winword.exe", "OpusApp");

    [Fact]
    public void FourSchemes_TheFifthIsRefused()
    {
        var (service, _, _, _) = Create(new AppSettings());

        var created = Enumerable.Range(0, 5).Select(i => service.Add("S" + i)).ToList();

        Assert.Equal([FilterId.Custom1, FilterId.Custom2, FilterId.Custom3, FilterId.Custom4], created.Take(4).Select(s => s!.Id));
        Assert.Null(created[4]);
        Assert.Equal(4, service.Schemes.Count);
    }

    [Fact]
    public void Update_AppliesLive_ShowTurnsItOnWhereTheMonitorTakesItsFilterFrom()
    {
        var (service, models, _, _) = Create(new AppSettings());
        var scheme = service.Add("Bernstein")!;

        service.Update(scheme with { Kind = SchemeKind.Tint, Hue = "#FF0000" });
        service.Show(scheme.Id, Display1);

        // Own filter per monitor is on from the factory: the monitor gets its own entry, the shared one stays.
        var shown = models.Effects.CurrentFor(Display1);
        Assert.Equal(new FilterSettings(true, FilterId.Custom1, 1, 0), shown.Filter);
        Assert.False(models.Effects.Current.Filter.Enabled);
        Assert.Equal(1, shown.Matrix.Apply(1, 1, 1).R, 0.001f);
        Assert.Equal(0, shown.Matrix.Apply(1, 1, 1).G, 0.001f);
    }

    [Fact]
    public void Delete_FallsBackEverywhere_StoredProfilesIncluded_TheFreedPlaceIsReused()
    {
        var settings = new AppSettings
        {
            ActiveProfile = "Standard",
            FilterEnabled = true,
            ColorFilter = FilterId.Custom1,
            CustomSchemes = [new CustomScheme { Id = FilterId.Custom1 }, new CustomScheme { Id = FilterId.Custom2 }],
            WindowFilter = WindowFilterOptions.Default.WithRule(WindowFilterRule.For(s_word, WindowFilterChoice.Filter, FilterId.Custom1)),
        };
        var (service, models, profiles, _) = Create(settings);
        profiles.Stored["Lesen"] = settings with { ColorFilter = FilterId.Custom1 };
        var video = settings with { ColorFilter = FilterId.Custom2 };
        profiles.Stored["Video"] = video;

        var fallbacks = service.Delete(FilterId.Custom1);

        Assert.Equal(3, fallbacks); // shared filter, Word rule, profile "Lesen"
        Assert.Equal(FilterId.Inverted, models.Effects.Current.Filter.Filter);
        Assert.Equal(FilterId.Inverted, models.Windows.Options.Rules[0].Filter);
        Assert.Equal(FilterId.Inverted, profiles.Stored["Lesen"].ColorFilter);

        // A profile that never chose the scheme is not rewritten; its copy of the schemes is ignored anyway.
        Assert.Same(video, profiles.Stored["Video"]);
        Assert.Equal([FilterId.Custom2], service.Schemes.Select(s => s.Id));
        Assert.Equal(FilterId.Inverted, profiles.Stored["Standard"].ColorFilter); // saved at once from the models
        Assert.Equal(FilterId.Custom1, service.Add("Neu")!.Id);
        Assert.Equal(0, service.Delete(FilterId.Custom4));
    }

    [Fact]
    public void ProfileSwitch_KeepsTheSchemes_TheyAreShared()
    {
        var (service, models, _, settings) = Create(new AppSettings());
        service.Add("Bernstein");

        settings.ApplyDisplay(new AppSettings { ColorFilter = FilterId.Custom1, FilterEnabled = true });

        Assert.Equal([FilterId.Custom1], models.Effects.Current.Schemes.Select(s => s.Id));
        Assert.Equal(FilterId.Custom1, models.Effects.Current.Filter.Filter);
    }

    [Fact]
    public void ViewModel_CreatesEditsAndDeletes_WithStatusTexts()
    {
        var (service, models, _, _) = Create(new AppSettings());
        var zoom = new ZoomCommands(models.Zoom, models.Effects, new OneMonitor(), new FixedCursor());
        var texts = AppTexts.CreateLocalizer();
        var vm = new CustomSchemesViewModel(service, new FilterNames(texts), zoom, texts);

        vm.Add();
        vm.Editor!.KindIndex = (int)SchemeKind.ReplaceOnly;
        vm.Editor.AddReplacement();
        vm.Editor.Name = "Rot zu Gelb";
        vm.ShowEditing();
        vm.Finish();

        Assert.Equal("Rot zu Gelb", Assert.Single(vm.Schemes).Name);
        var scheme = Assert.Single(models.Effects.Current.Schemes);
        Assert.Equal(SchemeKind.ReplaceOnly, scheme.Kind);
        Assert.Single(scheme.Replacements);
        Assert.Equal("#FFD600", vm.Editor.Preview[2].After); // red becomes the yellow of a new replacement
        Assert.Equal(FilterId.Custom1, models.Effects.CurrentFor(Display1).Filter.Filter);

        vm.DeleteEditing();

        Assert.Null(vm.Editor);
        Assert.True(vm.NoSchemes);
        Assert.Contains("Rot zu Gelb", vm.Status, StringComparison.Ordinal);
        Assert.Equal(FilterId.Inverted, models.Effects.CurrentFor(Display1).Filter.Filter);
    }

    private static (CustomSchemeService Service, RenderModels Models, MemoryProfiles Profiles, SettingsService Settings) Create(AppSettings settings)
    {
        var models = new RenderModels(
            ZoomMapping.Create(settings), EffectsMapping.Create(settings), TrackingMapping.Create(settings),
            new WindowFilterModel(settings.WindowFilter), new Core.Diagnostics.DebugSnapshots(), new SmartInvertHealth());
        var profiles = new MemoryProfiles();
        var service = new SettingsService(settings, models, new NoSettingsStore(), profiles);
        return (new CustomSchemeService(models, service, profiles, NullLogger<CustomSchemeService>.Instance), models, profiles, service);
    }

    private sealed class OneMonitor : IMonitorProvider
    {
        public IReadOnlyList<string> MonitorIds() => [Display1];

        public string MonitorAt(PhysicalPoint point) => Display1;

        public PhysicalRect? BoundsOf(string monitorId) => new PhysicalRect(0, 0, 1920, 1080);
    }

    private sealed class FixedCursor : ICursorSource
    {
        public bool TryGetPosition(out PhysicalPoint position)
        {
            position = new PhysicalPoint(100, 100);
            return true;
        }
    }

    private sealed class NoSettingsStore : ISettingsStore
    {
        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
        }
    }

    private sealed class MemoryProfiles : IProfileStore
    {
        public Dictionary<string, AppSettings> Stored { get; } = [];

        public IReadOnlyList<string> Names() => [.. Stored.Keys];

        public AppSettings? Load(string name) => Stored.GetValueOrDefault(name);

        public void Save(string name, AppSettings settings) => Stored[name] = settings;

        public void Delete(string name) => Stored.Remove(name);

        public bool Rename(string oldName, string newName) => false;

        public void Export(string name, string filePath)
        {
        }

        public string? Import(string filePath) => null;
    }
}
