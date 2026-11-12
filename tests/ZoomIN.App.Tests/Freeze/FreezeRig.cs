using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Freeze;
using ZoomIN.App.Localization;
using ZoomIN.App.Settings;
using ZoomIN.App.Tests.Overview;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Tests.Freeze;

/// <summary>A <see cref="MonitorFreezeController"/> with real models and fake monitors, cursor, fence and mover.</summary>
internal sealed class FreezeRig : IDisposable
{
    public FreezeRig(IMonitorProvider? monitors = null, AppSettings? settings = null)
    {
        settings ??= new AppSettings();
        Monitors = monitors ?? new ThreeMonitors();
        Models = new RenderModels(
            ZoomMapping.Create(settings), EffectsMapping.Create(settings), TrackingMapping.Create(settings),
            new WindowFilterModel(WindowFilterOptions.Default), new DebugSnapshots(), new SmartInvertHealth(),
            new OverviewModel(TimeProvider.System), new ViewCenterRequests());
        Settings = new SettingsService(settings, Models, new NullStore(), new NullProfiles(), WindowDragMapping.Create(settings, Models.Zoom));
        Mover = new FakeMover(Cursor, Fence);
        var texts = AppTexts.CreateLocalizer();
        texts.SetLanguage(AppLanguage.English);
        Switch = new MonitorSwitchController(
            Models.Zoom, Monitors, Cursor, Mover, Announcer, texts, NullLogger<MonitorSwitchController>.Instance);

        // Fence events run at once instead of on the thread pool.
        Controller = new MonitorFreezeController(
            Settings, Switch, Monitors, Cursor, Fence, Mover, Announcer, texts, NullLogger.Instance, work => work());
        Controller.Refused += Hints.Add;
        Switch.Refused += Hints.Add;
    }

    public IMonitorProvider Monitors { get; }

    public MovableCursor Cursor { get; } = new();

    public FakeFence Fence { get; } = new();

    public FakeMover Mover { get; }

    public RecordingAnnouncer Announcer { get; } = new();

    public List<string> Hints { get; } = [];

    public RenderModels Models { get; }

    public SettingsService Settings { get; }

    public MonitorSwitchController Switch { get; }

    public MonitorFreezeController Controller { get; }

    public MonitorFreeze Frozen => Models.Zoom.Frozen;

    /// <summary>The monitor the pointer is on now.</summary>
    public string PointerMonitor => Monitors.MonitorAt(Cursor.Position);

    public void PointerTo(string monitor) => Cursor.Position = ThreeMonitors.MiddleOf(monitor);

    public void Dispose()
    {
        Controller.Dispose();
        Settings.Dispose();
    }

    private sealed class NullStore : ISettingsStore
    {
        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
        }
    }

    private sealed class NullProfiles : IProfileStore
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
