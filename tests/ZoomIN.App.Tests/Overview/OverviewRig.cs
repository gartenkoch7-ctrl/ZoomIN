using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Localization;
using ZoomIN.App.Overview;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Tests.Overview;

/// <summary>An <see cref="OverviewController"/> with real models and fake Windows services.</summary>
internal sealed class OverviewRig : IDisposable
{
    public OverviewRig(AppSettings? settings = null)
    {
        settings ??= new AppSettings();
        Models = new RenderModels(
            ZoomMapping.Create(settings), EffectsMapping.Create(settings), TrackingMapping.Create(settings),
            new WindowFilterModel(WindowFilterOptions.Default), new Core.Diagnostics.DebugSnapshots(), new SmartInvertHealth(),
            new OverviewModel(Clock), new ViewCenterRequests());
        Settings = new SettingsService(settings, Models, new NullSettingsStore(), new NullProfileStore(),
            WindowDragMapping.Create(settings, Models.Zoom));
        Commands = new ZoomCommands(Models.Zoom, Models.Effects, Monitors, Cursor);
        var texts = AppTexts.CreateLocalizer();
        texts.SetLanguage(AppLanguage.English);
        Controller = new OverviewController(
            Settings, Commands, Monitors, Cursor, Keys, Announcer, texts, Clock, NullLogger<OverviewController>.Instance);
        Debug = new OverviewDebug(Controller, Settings);
    }

    public ManualClock Clock { get; } = new();

    public MovableCursor Cursor { get; } = new();

    public TwoMonitors Monitors { get; } = new();

    public FakeOverviewKeys Keys { get; } = new();

    public RecordingAnnouncer Announcer { get; } = new();

    public RenderModels Models { get; }

    public SettingsService Settings { get; }

    public ZoomCommands Commands { get; }

    public OverviewController Controller { get; }

    public OverviewDebug Debug { get; }

    public ZoomModel Zoom => Models.Zoom;

    public OverviewModel Overview => Models.Overview;

    public double? Level(string monitor = TwoMonitors.First) => Zoom.Temporary.LevelFor(TemporaryZoomOwner.Overview, monitor);

    public void Dispose()
    {
        Controller.Dispose();
        Settings.Dispose();
    }

    private sealed class NullSettingsStore : ISettingsStore
    {
        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
        }
    }

    private sealed class NullProfileStore : IProfileStore
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
