using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Markers;
using ZoomIN.App.Settings;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Tests.Markers;

/// <summary>
/// The hold key hook runs all the time, since holding the key always lifts the zoom to 1×
/// (<see cref="HoldKeyZoom"/>); tested with a fake hook (ADR-0025).
/// </summary>
public sealed class MarkerInputTests
{
    [Fact]
    public void FactorySetting_StartsTheHook_WithTheChosenKey()
    {
        var (input, hook, settings) = Create(new AppSettings { MarkerHoldKey = MarkerHoldKey.LeftCtrl });

        input.Start();

        Assert.True(hook.Running);
        Assert.Equal(MarkerHoldKey.LeftCtrl, hook.Handler!.Key);
        Assert.Same(settings.Models.Markers, hook.Handler);
    }

    [Fact]
    public void ConditionChanges_KeepTheHookRunning()
    {
        var (input, hook, settings) = Create(new AppSettings { CrosshairEnabled = true, CrosshairShow = ShowCondition.HoldKey });
        input.Start();

        var effects = settings.Models.Effects;
        effects.SetCursor(effects.Current.Cursor with { CrosshairShow = ShowCondition.Idle });

        Assert.True(hook.Running);
        Assert.Equal(1, hook.Starts);
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
        var settings = MarkerTestSettings.Create(initial);
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
}
