using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Localization;
using ZoomIN.App.ViewModels;
using ZoomIN.Core.Pointer;
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

    [Fact]
    public void CaretLookAndShowConditions_ChangeOnlyThemselves()
    {
        // M18 review: a profile switch while the page is open must not be undone by the caret settings.
        var tracking = new TrackingModel(TrackingOptions.Default);
        var vm = new TrackingViewModel(tracking, AppTexts.CreateLocalizer(), NullLogger<TrackingViewModel>.Instance);
        var profile = TrackingOptions.Default with
        {
            Enabled = false, FocusFrame = false, MouseFollow = MouseFollowMode.Fixpoint,
            CaretStyle = CaretMarkerStyle.Default with { Color = RgbColor.ParseOrDefault("#00E5FF", RgbColor.Magenta) },
        };
        tracking.SetOptions(profile);

        vm.CaretShapeIndex = (int)CaretMarkerShape.Wedges;
        vm.CaretShowIndex = (int)ShowCondition.HoldKey;
        vm.CaretShapeIndex = -1; // the ComboBox unloads

        var o = tracking.Options;
        Assert.Equal((false, false, MouseFollowMode.Fixpoint), (o.Enabled, o.FocusFrame, o.MouseFollow));
        Assert.Equal(profile.CaretStyle with { Shape = CaretMarkerShape.Wedges }, o.CaretStyle);
        Assert.Equal(ShowCondition.HoldKey, o.CaretShow);
        Assert.Equal(CaretMarkerShape.Wedges, vm.CaretStyle.Shape);
    }
}
