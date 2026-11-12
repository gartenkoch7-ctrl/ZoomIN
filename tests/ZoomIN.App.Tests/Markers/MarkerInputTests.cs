using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Markers;
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

/// <summary>The hold key hook runs only while a highlight waits for it (E1); tested with a fake hook (ADR-0025).</summary>
public sealed class MarkerInputTests
{
    [Fact]
    public void FactorySetting_NoHookRuns()
    {
        var (input, hook, _) = Create(new AppSettings());

        input.Start();

        Assert.False(hook.Running);
    }

    [Fact]
    public void ConditionHoldKey_StartsTheHook_WithTheChosenKey()
    {
        var (input, hook, settings) = Create(new AppSettings { MarkerHoldKey = MarkerHoldKey.LeftCtrl });
        input.Start();

        settings.Models.Tracking.SetOptions(settings.Models.Tracking.Options with { CaretShow = ShowCondition.HoldKey });

        Assert.True(hook.Running);
        Assert.Equal(MarkerHoldKey.LeftCtrl, hook.Handler!.Key);
        Assert.Same(settings.Models.Markers, hook.Handler);
    }

    [Fact]
    public void CrosshairOnHoldKey_FromTheProfile_StartsAtOnce_AndStopsWhenNoLongerNeeded()
    {
        var (input, hook, settings) = Create(new AppSettings { CrosshairEnabled = true, CrosshairShow = ShowCondition.HoldKey });
        input.Start();
        Assert.True(hook.Running);

        var effects = settings.Models.Effects;
        effects.SetCursor(effects.Current.Cursor with { CrosshairShow = ShowCondition.Idle });

        Assert.False(hook.Running);
    }

    [Fact]
    public void HoldKeyConditionOnASwitchedOffHighlight_NeedsNoHook()
    {
        var (input, hook, _) = Create(new AppSettings
        {
            CrosshairShow = ShowCondition.HoldKey, // crosshair off from the factory
            CaretMarker = false,
            CaretMarkerShow = ShowCondition.HoldKey,
        });

        input.Start();

        Assert.False(hook.Running);
    }

    [Fact]
    public void RefusedHook_IsRetriedOnAConditionChange_NotOnEverySliderStep()
    {
        var (input, hook, settings) = Create(new AppSettings { FocusFrameShow = ShowCondition.HoldKey });
        hook.Refuse = true;
        input.Start();
        Assert.Equal(1, hook.Starts);

        var effects = settings.Models.Effects;
        effects.SetCursor(effects.Current.Cursor with { CrosshairWidth = 4 }); // a slider step
        Assert.Equal(1, hook.Starts);

        hook.Refuse = false;
        var tracking = settings.Models.Tracking;
        tracking.SetOptions(tracking.Options with { CaretShow = ShowCondition.Idle });

        Assert.Equal(2, hook.Starts);
        Assert.True(hook.Running);
    }

    [Fact]
    public void ChangingTheSharedHoldKey_ReachesTheHook()
    {
        var (input, hook, settings) = Create(new AppSettings { FocusFrameShow = ShowCondition.HoldKey });
        input.Start();

        settings.Update(s => s with { MarkerHoldKey = MarkerHoldKey.RightAlt });

        Assert.Equal(MarkerHoldKey.RightAlt, hook.Handler!.Key);
    }

    [Fact]
    public void Dispose_StopsTheHook_AndIgnoresLaterChanges()
    {
        var (input, hook, settings) = Create(new AppSettings { FocusFrameShow = ShowCondition.HoldKey });
        input.Start();

        input.Dispose();
        settings.Models.Tracking.SetOptions(settings.Models.Tracking.Options with { CaretShow = ShowCondition.HoldKey });

        Assert.False(hook.Running);
    }

    private static (MarkerInput Input, FakeHoldKeySource Hook, SettingsService Settings) Create(AppSettings initial)
    {
        var models = new RenderModels(
            ZoomMapping.Create(initial), EffectsMapping.Create(initial), TrackingMapping.Create(initial),
            new WindowFilterModel(WindowFilterOptions.Default), new Core.Diagnostics.DebugSnapshots(), new SmartInvertHealth(),
            new OverviewModel(TimeProvider.System), new ViewCenterRequests());
        var settings = new SettingsService(initial, models, new NoSettingsStore(), new NoProfiles(), WindowDragMapping.Create(initial, models.Zoom));
        var hook = new FakeHoldKeySource();
        return (new MarkerInput(settings, hook, NullLogger<MarkerInput>.Instance), hook, settings);
    }

    private sealed class FakeHoldKeySource : IHoldKeySource
    {
        public bool Running { get; private set; }

        public IHoldKeyHandler? Handler { get; private set; }

        public bool Refuse { get; set; }

        public int Starts { get; private set; }

        public bool Start(IHoldKeyHandler handler)
        {
            Starts++;
            Running = !Refuse;
            Handler = handler;
            return Running;
        }

        public void Stop() => Running = false;

        public void Dispose() => Stop();
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
