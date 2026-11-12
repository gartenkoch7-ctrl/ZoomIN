using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Islands;
using ZoomIN.App.Localization;
using ZoomIN.App.Settings;
using ZoomIN.App.Tests.Overview;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Tests.Islands;

/// <summary>An <see cref="IslandController"/> with real models and fake input, fence and timers; two monitors.</summary>
internal sealed class IslandRig : IDisposable
{
    public IslandRig(AppSettings? settings = null)
    {
        settings ??= new AppSettings();
        Models = new RenderModels(
            ZoomMapping.Create(settings), EffectsMapping.Create(settings), TrackingMapping.Create(settings),
            new WindowFilterModel(WindowFilterOptions.Default), new Core.Diagnostics.DebugSnapshots(), new SmartInvertHealth(),
            new OverviewModel(TimeProvider.System), new ViewCenterRequests())
        {
            Islands = IslandMapping.Create(settings),
        };
        Settings = new SettingsService(settings, Models, new Store(), new Profiles(), WindowDragMapping.Create(settings, Models.Zoom));
        var texts = AppTexts.CreateLocalizer();
        texts.SetLanguage(AppLanguage.English);
        Controller = new IslandController(Settings, Monitors, Input, Fence, Announcer, texts, Clock, NullLogger<IslandController>.Instance);
        Visibility = new IslandVisibility(Settings, Announcer, texts, NullLogger<IslandVisibility>.Instance);
    }

    public TwoMonitors Monitors { get; } = new();

    public FakeIslandInput Input { get; } = new();

    public FakeFence Fence { get; } = new();

    public RecordingAnnouncer Announcer { get; } = new();

    public TimerClock Clock { get; } = new();

    public RenderModels Models { get; }

    public SettingsService Settings { get; }

    public IslandController Controller { get; }

    public IslandVisibility Visibility { get; }

    public IslandModel Islands => Models.Islands;

    public IIslandEditInputHandler Handler => Input.Handler ?? throw new InvalidOperationException("The edit mode does not take the input.");

    public double? Level(string monitor) => Models.Zoom.Temporary.LevelFor(TemporaryZoomOwner.IslandEdit, monitor);

    public void Dispose()
    {
        Controller.Dispose();
        Settings.Dispose();
    }

    private sealed class Store : ISettingsStore
    {
        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
        }
    }

    private sealed class Profiles : IProfileStore
    {
        public IReadOnlyList<string> Names() => [];

        public AppSettings? Load(string name) => null;

        public void Save(string name, AppSettings settings)
        {
        }

        public void Delete(string name)
        {
        }

        public bool Rename(string oldName, string newName) => false;

        public void Export(string name, string filePath)
        {
        }

        public string? Import(string filePath) => null;
    }
}
