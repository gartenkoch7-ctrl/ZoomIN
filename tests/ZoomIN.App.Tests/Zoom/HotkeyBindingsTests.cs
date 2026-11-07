using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Tests.Zoom;

/// <summary>The shortcut table (M14 step 10, split out of ZoomController), with fake Windows services.</summary>
public sealed class HotkeyBindingsTests
{
    [Fact]
    public void Register_BindsTheShortcuts_AndToggleZoomWorks()
    {
        var (bindings, hotkeys, models) = Create(new AppSettings());

        bindings.Register();

        Assert.Contains(HotkeyGesture.ToggleZoom, hotkeys.Registered.Keys);
        Assert.Contains(HotkeyGesture.ZoomIn, hotkeys.Registered.Keys);
        Assert.True(hotkeys.Registered[HotkeyGesture.ZoomIn].Repeat);
        Assert.False(hotkeys.Registered[HotkeyGesture.ToggleZoom].Repeat);

        hotkeys.Registered[HotkeyGesture.ToggleZoom].Action();

        Assert.True(models.Zoom.IsOn);
    }

    [Fact]
    public void UnassignedShortcut_IsSkipped_AndTakenOnes_AreReported()
    {
        var settings = new AppSettings() with { WindowFilterHotkey = default };
        var (bindings, hotkeys, _) = Create(settings, taken: HotkeyGesture.ZoomOut);

        bindings.Register();

        Assert.DoesNotContain(default, hotkeys.Registered.Keys);
        Assert.Equal([HotkeyGesture.ZoomOut], bindings.Unavailable);
    }

    [Fact]
    public void Register_Again_StartsFromScratch_AndSuspend_ReleasesAll()
    {
        var (bindings, hotkeys, _) = Create(new AppSettings());
        bindings.Register();
        var count = hotkeys.Registered.Count;

        bindings.Register();

        Assert.Equal(count, hotkeys.Registered.Count);

        bindings.Suspend();

        Assert.Empty(hotkeys.Registered);
    }

    private static (HotkeyBindings Bindings, FakeHotkeys Hotkeys, RenderModels Models) Create(
        AppSettings settings, HotkeyGesture? taken = null)
    {
        var models = new RenderModels(
            ZoomMapping.Create(settings), EffectsMapping.Create(settings), TrackingMapping.Create(settings),
            new WindowFilterModel(WindowFilterOptions.Default), new Core.Diagnostics.DebugSnapshots(), new SmartInvertHealth());
        var service = new SettingsService(settings, models, new NoSettingsStore(), new NoProfiles());
        var monitors = new OneMonitor();
        var zoom = new ZoomCommands(models.Zoom, models.Effects, monitors, new FixedCursor());
        var windowFilter = new WindowFilterCommands(models.Windows, models.Effects, new NoWindows(), monitors);
        var hotkeys = new FakeHotkeys(taken);
        var bindings = new HotkeyBindings(
            service, new ProfileService(service, new NoProfiles()), hotkeys, zoom, windowFilter, NullLogger<HotkeyBindings>.Instance);
        return (bindings, hotkeys, models);
    }

    private sealed class FakeHotkeys(HotkeyGesture? taken) : IHotkeyService
    {
        public Dictionary<HotkeyGesture, (Action Action, bool Repeat)> Registered { get; } = [];

        public bool Register(HotkeyGesture gesture, Action onPressed, bool repeat = false)
        {
            if (gesture == taken)
            {
                return false;
            }

            Registered[gesture] = (onPressed, repeat);
            return true;
        }

        public void UnregisterAll() => Registered.Clear();

        public void Dispose()
        {
        }
    }

    private sealed class OneMonitor : IMonitorProvider
    {
        public IReadOnlyList<string> MonitorIds() => [@"\\.\DISPLAY1"];

        public string MonitorAt(PhysicalPoint point) => @"\\.\DISPLAY1";

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

    private sealed class NoWindows : IWindowFilterSource
    {
        public void Start(WindowFilterModel model)
        {
        }

        public bool TryGetForeground(out WindowFilterTarget window)
        {
            window = default;
            return false;
        }

        public IReadOnlyList<WindowKind> OpenKinds() => [];

        public void Dispose()
        {
        }
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
