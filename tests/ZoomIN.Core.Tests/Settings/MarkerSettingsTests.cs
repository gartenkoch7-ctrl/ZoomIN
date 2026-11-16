using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.Settings;

/// <summary>Show conditions (per profile) and hold key (shared) of the highlights in the settings (E1).</summary>
public sealed class MarkerSettingsTests
{
    private static readonly AppSettings s_chosen = new()
    {
        CrosshairShow = ShowCondition.HoldKey,
        CaretMarkerShow = ShowCondition.Idle,
        FocusFrameShow = ShowCondition.Moving,
        MarkerHoldKey = MarkerHoldKey.LeftAlt,
    };

    [Fact]
    public void FactorySetting_IsAlwaysAndRightShift()
    {
        var settings = new AppSettings();

        Assert.Equal(ShowCondition.Always, PointerMapping.Style(settings).CrosshairShow);
        Assert.Equal(ShowCondition.Always, TrackingMapping.Options(settings).CaretShow);
        Assert.Equal(ShowCondition.Always, TrackingMapping.Options(settings).FocusFrameShow);
        Assert.Equal(MarkerHoldKey.RightShift, settings.MarkerHoldKey);
    }

    [Fact]
    public void Conditions_RoundTripThroughTheModels()
    {
        var effects = EffectsMapping.Apply(new AppSettings(), EffectsMapping.Create(s_chosen));
        var tracking = TrackingMapping.Apply(new AppSettings(), TrackingMapping.Create(s_chosen));

        Assert.Equal(ShowCondition.HoldKey, effects.CrosshairShow);
        Assert.Equal(ShowCondition.Idle, tracking.CaretMarkerShow);
        Assert.Equal(ShowCondition.Moving, tracking.FocusFrameShow);
    }

    [Fact]
    public void Conditions_BelongToTheProfile_HoldKeyIsShared()
    {
        var result = ProfileFields.ApplyDisplay(new AppSettings { MarkerHoldKey = MarkerHoldKey.RightCtrl }, s_chosen);

        Assert.Equal(ShowCondition.HoldKey, result.CrosshairShow);
        Assert.Equal(ShowCondition.Idle, result.CaretMarkerShow);
        Assert.Equal(ShowCondition.Moving, result.FocusFrameShow);
        Assert.Equal(MarkerHoldKey.RightCtrl, result.MarkerHoldKey);
    }

    [Fact]
    public void UnknownConditions_FallBackToAlways()
    {
        var settings = new AppSettings
        {
            CrosshairShow = (ShowCondition)9,
            CaretMarkerShow = (ShowCondition)9,
            FocusFrameShow = (ShowCondition)9,
        };

        Assert.Equal(ShowCondition.Always, PointerMapping.Style(settings).CrosshairShow);
        Assert.Equal(ShowCondition.Always, TrackingMapping.Options(settings).CaretShow);
        Assert.Equal(ShowCondition.Always, TrackingMapping.Options(settings).FocusFrameShow);
    }

    [Fact]
    public void SecureDesktop_ShowsEveryHighlightAlways()
    {
        var result = SecureSettingsSanitizer.Sanitize(s_chosen with { MarkerHoldKey = (MarkerHoldKey)77 });

        Assert.Equal(ShowCondition.Always, result.CrosshairShow);
        Assert.Equal(ShowCondition.Always, result.CaretMarkerShow);
        Assert.Equal(ShowCondition.Always, result.FocusFrameShow);
        Assert.Equal(MarkerHoldKey.RightShift, result.MarkerHoldKey);
    }
}
