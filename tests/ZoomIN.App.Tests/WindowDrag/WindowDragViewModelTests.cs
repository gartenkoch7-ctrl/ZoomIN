using ZoomIN.App.ViewModels;
using ZoomIN.Core.Modes;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Tests.WindowDrag;

/// <summary>The settings card for zoom while moving windows (G1).</summary>
public sealed class WindowDragViewModelTests
{
    [Fact]
    public void ShowsTheOptions_AndAppliesEachChangeLive()
    {
        using var drag = Create(new WindowDragOptions { Action = WindowDragAction.Lower, LowerLevel = 3 });
        var vm = new WindowDragViewModel(drag);
        var saved = 0;
        drag.OptionsChanged += () => saved++;

        Assert.Equal((int)WindowDragAction.Lower, vm.ActionIndex);
        Assert.True(vm.IsLower && vm.Acts);

        vm.ActionIndex = (int)WindowDragAction.Unchanged;
        vm.LowerLevel = 4;
        vm.OnMove = false;
        vm.OnResize = false;
        Assert.False(vm.IsLower || vm.Acts);
        vm.ActionIndex = -1; // combo box while its items are replaced: ignored

        Assert.Equal(new WindowDragOptions { Action = WindowDragAction.Unchanged, LowerLevel = 4, OnMove = false, OnResize = false }, drag.Options);
        Assert.Equal(4, saved);
    }

    [Fact]
    public void Change_KeepsTheOtherFields_OfANewerProfile()
    {
        using var drag = Create(WindowDragOptions.Default);
        var vm = new WindowDragViewModel(drag);

        // A profile switch while the page is open: the page still shows the old values.
        drag.Options = new WindowDragOptions { Action = WindowDragAction.Lower, LowerLevel = 3, OnResize = false };
        vm.OnMove = false;

        Assert.Equal(new WindowDragOptions { Action = WindowDragAction.Lower, LowerLevel = 3, OnMove = false, OnResize = false }, drag.Options);
    }

    private static WindowDragZoom Create(WindowDragOptions options) =>
        new(new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, perMonitor: true, 0.15), options);
}
