using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Localization;
using ZoomIN.App.ViewModels;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Tests.Settings;

/// <summary>The mouse follow choice on "Fokus &amp; Verfolgung" (M17 A1).</summary>
public sealed class TrackingViewModelTests
{
    [Fact]
    public void ComboBoxWithoutSelection_ShowsTheStoredModeAgain_AndChangesNothing()
    {
        var tracking = new TrackingModel(TrackingOptions.Default with { MouseFollow = MouseFollowMode.Fixpoint });
        var vm = new TrackingViewModel(tracking, AppTexts.CreateLocalizer(), NullLogger<TrackingViewModel>.Instance);

        vm.MouseFollowIndex = -1;

        Assert.Equal((int)MouseFollowMode.Fixpoint, vm.MouseFollowIndex);
        Assert.False(vm.MouseMarginEnabled);
        Assert.Equal(MouseFollowMode.Fixpoint, tracking.Options.MouseFollow);
    }

    [Fact]
    public void ComboBoxWithoutSelection_DoesNotWriteOlderPageValuesBack()
    {
        var tracking = new TrackingModel(TrackingOptions.Default);
        var vm = new TrackingViewModel(tracking, AppTexts.CreateLocalizer(), NullLogger<TrackingViewModel>.Instance);
        tracking.ToggleEnabled(); // Strg+Alt+T while the page is open

        vm.MouseFollowIndex = -1;
        Assert.False(tracking.Options.Enabled);

        vm.MouseFollowIndex = (int)MouseFollowMode.Fixpoint;
        vm.MouseMargin = 30;

        Assert.False(tracking.Options.Enabled);
        Assert.Equal(MouseFollowMode.Fixpoint, tracking.Options.MouseFollow);
        Assert.Equal(0.3, tracking.Options.MouseMargin, 6);
    }

    [Fact]
    public void ChoosingTheMargin_AppliesIt()
    {
        var tracking = new TrackingModel(TrackingOptions.Default with { MouseFollow = MouseFollowMode.Center });
        var vm = new TrackingViewModel(tracking, AppTexts.CreateLocalizer(), NullLogger<TrackingViewModel>.Instance);

        vm.MouseFollowIndex = (int)MouseFollowMode.Margin;

        Assert.True(vm.MouseMarginEnabled);
        Assert.Equal(MouseFollowMode.Margin, tracking.Options.MouseFollow);
    }
}
