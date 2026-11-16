using ZoomIN.Core.Filters;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Updates;
using ZoomIN.Setup.Install;
using ZoomIN.Setup.Localization;

namespace ZoomIN.Setup.Tests.Install;

/// <summary>What the wizard writes into the user's settings, and its texts (M14 step 11).</summary>
public sealed class SetupChoicesTests
{
    [Fact]
    public void Choices_AreMerged_AndTheFirstRunIsDone()
    {
        var existing = new AppSettings() with { ColorFilter = FilterId.Grayscale, LensWidth = 0.55 };
        var choices = new SetupChoices(ReleaseChannel.Experimental, Autostart: true, DesktopShortcut: false, CheckForUpdates: true,
            ZoomLevel: 3, Filter: FilterId.InvertedSmart);

        var settings = choices.ApplyTo(existing);

        Assert.Equal(ReleaseChannel.Experimental, settings.UpdateChannel);
        Assert.True(settings.CheckForUpdates);
        Assert.Equal(3, settings.LastZoomLevel);
        Assert.True(settings.FilterEnabled);
        Assert.Equal(FilterId.InvertedSmart, settings.ColorFilter);
        Assert.True(settings.FirstRunCompleted);
        Assert.Equal(0.55, settings.LensWidth); // a reinstall keeps everything else
    }

    [Fact]
    public void NoFilter_TurnsTheFilterOff_ButKeepsTheKind()
    {
        var existing = new AppSettings() with { FilterEnabled = true, ColorFilter = FilterId.Grayscale };

        var settings = new SetupChoices(ReleaseChannel.Stable, false, false, false, 2, Filter: null).ApplyTo(existing);

        Assert.False(settings.FilterEnabled);
        Assert.Equal(FilterId.Grayscale, settings.ColorFilter);
    }

    [Fact]
    public void Texts_SwitchLanguage_ThroughTheFacade()
    {
        var wasGerman = SetupText.IsGerman;
        try
        {
            SetupText.SetGerman(true);
            Assert.True(SetupText.IsGerman);

            SetupText.SetGerman(false);
            Assert.False(SetupText.IsGerman);
            Assert.Equal("NoSuchKey", SetupText.Get("NoSuchKey"));
        }
        finally
        {
            SetupText.SetGerman(wasGerman);
        }
    }
}
