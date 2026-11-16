using ZoomIN.App.Settings;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Tests.Markers;

/// <summary>A settings service without files for the hold key tests (ADR-0025).</summary>
internal static class MarkerTestSettings
{
    /// <param name="markers">The hold key state, e.g. on a test clock; a fresh one if null.</param>
    public static SettingsService Create(AppSettings initial, MarkerActivity? markers = null)
    {
        var models = new RenderModels(
            ZoomMapping.Create(initial), EffectsMapping.Create(initial), TrackingMapping.Create(initial),
            new WindowFilterModel(WindowFilterOptions.Default), new Core.Diagnostics.DebugSnapshots(), new SmartInvertHealth(),
            new OverviewModel(TimeProvider.System), new ViewCenterRequests())
        {
            Markers = markers ?? new MarkerActivity(),
        };
        return new SettingsService(initial, models, new NoSettingsStore(), new NoProfiles(), WindowDragMapping.Create(initial, models.Zoom));
    }

    private sealed class NoSettingsStore : ISettingsStore
    {
        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
        }
    }

    private sealed class NoProfiles : IProfileStore
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
