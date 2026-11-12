using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Filters;

/// <summary>"Schärfe" and "Fett" (M18, D1): ranges, where they apply, settings and profile.</summary>
public sealed class TextEnhancementTests
{
    private static readonly TextEnhancement s_on = new(0.6, 1);

    [Fact]
    public void FactoryDefault_IsOff()
    {
        Assert.False(DisplayEffects.Default.Enhancement.IsActive);
        Assert.False(EffectsMapping.Effects(new AppSettings()).Enhancement.IsActive);
        Assert.Equal(default(TextEnhancement), TextEnhancement.Off);
    }

    [Theory]
    [InlineData(0.5, 0, true)]
    [InlineData(0, 0.1, true)]
    [InlineData(0, 0, false)]
    public void IsActive_WhenEitherIsAboveZero(double sharpness, double bold, bool active)
    {
        Assert.Equal(active, new TextEnhancement(sharpness, bold).IsActive);
    }

    [Theory]
    [InlineData(-1, -2, 0, 0)]
    [InlineData(2, 5, 1, 3)]
    [InlineData(0.4, 2.5, 0.4, 2.5)]
    [InlineData(double.NaN, double.PositiveInfinity, 0, 0)]
    [InlineData(0.004, 0.04, 0, 0)] // below the slider steps: shown as 0, so off
    [InlineData(0.456, 1.26, 0.46, 1.3)]
    public void Normalized_ClampsToTheSliderRanges(double sharpness, double bold, double wantSharpness, double wantBold)
    {
        var normalized = new TextEnhancement(sharpness, bold).Normalized();

        Assert.Equal(wantSharpness, normalized.Sharpness);
        Assert.Equal(wantBold, normalized.BoldPixels);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1.49, false)]
    [InlineData(1.4999999999, true)]
    [InlineData(1.5, true)]
    [InlineData(8, true)]
    public void At_AppliesFromOnePointFiveTimes(double zoom, bool applies)
    {
        Assert.Equal(applies ? s_on : TextEnhancement.Off, s_on.At(zoom, Smoothing.Text));
    }

    [Theory]
    [InlineData(Smoothing.Text, true)]
    [InlineData(Smoothing.Soft, false)]
    [InlineData(Smoothing.Pixel, false)]
    public void At_AppliesOnlyWithTextOptimisedSmoothing(Smoothing smoothing, bool applies)
    {
        Assert.Equal(applies, TextEnhancement.WorksWith(smoothing));
        Assert.Equal(applies ? s_on : TextEnhancement.Off, s_on.At(4, smoothing));
    }

    [Theory]
    [InlineData(1344, 1080, 576, 900, false)] // docked left 0.7 at 1.2×: 2.33× across, only 1.2× down
    [InlineData(1920, 1080, 960, 540, true)] // fullscreen 2×
    public void At_Frame_TheLessMagnifiedAxisCounts(double targetWidth, double targetHeight, double sourceWidth, double sourceHeight, bool applies)
    {
        var frame = new ModeFrame(new PhysicalRect(0, 0, targetWidth, targetHeight), new PhysicalRect(0, 0, sourceWidth, sourceHeight), false);

        Assert.Equal(applies ? s_on : TextEnhancement.Off, s_on.At(frame, Smoothing.Text));
    }

    [Fact]
    public void At_Off_StaysOff()
    {
        Assert.Equal(TextEnhancement.Off, TextEnhancement.Off.At(8, Smoothing.Text));
    }

    [Fact]
    public void SetEnhancement_Clamps_ReachesEveryMonitor_AndRaisesChangedOncePerChange()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        model.SetMonitorFilter("m", new MonitorFilter(true, FilterId.Inverted));
        var raised = 0;
        model.Changed += () => raised++;

        model.SetEnhancement(new TextEnhancement(1.5, 4));
        model.SetEnhancement(new TextEnhancement(1, 3));

        Assert.Equal(new TextEnhancement(1, 3), model.Current.Enhancement);
        Assert.Equal(new TextEnhancement(1, 3), model.CurrentFor("m").Enhancement);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Replace_TakesTheProfilesValues_Normalized()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        var profile = new DisplayEffects(FilterSettings.Default, Smoothing.Text, PointerStyle.Default) { Enhancement = new TextEnhancement(-1, 9) };

        model.Replace(profile);

        Assert.Equal(new TextEnhancement(0, 3), model.Current.Enhancement);
    }

    [Fact]
    public void Mapping_ClampsOutOfRangeFileValues()
    {
        var effects = EffectsMapping.Effects(new AppSettings { TextSharpness = 7, TextBold = -1 });

        Assert.Equal(new TextEnhancement(1, 0), effects.Enhancement);
    }

    [Fact]
    public void Profile_CarriesBothValues()
    {
        var applied = ProfileFields.ApplyDisplay(new AppSettings(), new AppSettings { TextSharpness = 0.3, TextBold = 2 });

        Assert.Equal(0.3, applied.TextSharpness);
        Assert.Equal(2, applied.TextBold);
    }

    [Theory]
    [InlineData(double.NaN, double.NaN, 0, 0)]
    [InlineData(5, 10, 1, 3)]
    [InlineData(0.5, 1.5, 0.5, 1.5)]
    public void SecureDesktop_BoundsTheValues(double sharpness, double bold, double wantSharpness, double wantBold)
    {
        var sanitized = SecureSettingsSanitizer.Sanitize(new AppSettings { TextSharpness = sharpness, TextBold = bold });

        Assert.Equal(wantSharpness, sanitized.TextSharpness);
        Assert.Equal(wantBold, sanitized.TextBold);
    }
}
