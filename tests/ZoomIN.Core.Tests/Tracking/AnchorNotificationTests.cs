using System.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

public sealed class AnchorNotificationTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly PhysicalPoint s_mouse = new(300, 300);
    private static readonly PhysicalRect s_toast = new(1540, 820, 360, 200);
    private static readonly PhysicalPoint s_toastCenter = new(1720, 920);
    private static readonly TrackingOptions s_instant = TrackingOptions.Default with { PanSeconds = 0 };

    [Fact]
    public void NewToast_PansToItsCenter()
    {
        var controller = Started();

        var anchor = controller.Next(Input(5.0, s_mouse, Toast(4.9)));

        Assert.Equal(s_toastCenter, anchor);
        Assert.Equal(AnchorSource.Notification, controller.Source);
    }

    [Fact]
    public void ToastCloses_ViewReturnsToMouse()
    {
        var controller = Started();
        controller.Next(Input(5.0, s_mouse, Toast(4.9)));

        var anchor = controller.Next(Input(6.0, s_mouse, TrackingSnapshot.Empty));

        Assert.Equal(s_mouse, anchor);
        Assert.Equal(AnchorSource.Mouse, controller.Source);
    }

    [Fact]
    public void ToastStillVisible_ViewStays()
    {
        var controller = Started();
        controller.Next(Input(5.0, s_mouse, Toast(4.9)));

        var anchor = controller.Next(Input(8.0, s_mouse, Toast(4.9)));

        Assert.Equal(s_toastCenter, anchor);
    }

    [Fact]
    public void MouseMovement_TakesTheViewBack()
    {
        var controller = Started();
        controller.Next(Input(5.0, s_mouse, Toast(4.9)));

        var moved = new PhysicalPoint(330, 300);
        var anchor = controller.Next(Input(6.0, moved, Toast(4.9)));

        Assert.Equal(moved, anchor);
        Assert.Equal(AnchorSource.Mouse, controller.Source);
    }

    [Fact]
    public void ToastWhileMouseMoves_PansOnceTheMouseRests()
    {
        var controller = Started();
        var moving = new PhysicalPoint(320, 300);
        controller.Next(Input(4.8, moving));

        Assert.Equal(moving, controller.Next(Input(5.0, moving, Toast(4.9))));
        Assert.Equal(s_toastCenter, controller.Next(Input(5.5, moving, Toast(4.9))));
    }

    [Fact]
    public void OptionOff_IgnoresToast()
    {
        var controller = Started();

        var anchor = controller.Next(Input(5.0, s_mouse, Toast(4.9), s_instant with { Notifications = false }));

        Assert.Equal(s_mouse, anchor);
    }

    [Fact]
    public void FocusTrackingOff_StillFollowsToast()
    {
        var controller = Started();

        var anchor = controller.Next(Input(5.0, s_mouse, Toast(4.9), s_instant with { Enabled = false }));

        Assert.Equal(s_toastCenter, anchor);
    }

    [Fact]
    public void Hotkey_PansEvenWhenOptionOff()
    {
        var controller = Started();
        var options = s_instant with { Notifications = false };
        controller.Next(Input(5.0, s_mouse, Toast(4.9), options));

        var anchor = controller.Next(Input(6.0, s_mouse, Toast(4.9), options, requestedAt: 5.9));

        Assert.Equal(s_toastCenter, anchor);
        Assert.Equal(AnchorSource.Notification, controller.Source);
    }

    [Fact]
    public void Hotkey_WithoutToast_DoesNothing()
    {
        var controller = Started();

        var anchor = controller.Next(Input(6.0, s_mouse, TrackingSnapshot.Empty, requestedAt: 5.9));

        Assert.Equal(s_mouse, anchor);
        Assert.Equal(AnchorSource.Mouse, controller.Source);
    }

    [Fact]
    public void ToastOnOtherMonitor_IsIgnored()
    {
        var controller = Started();
        var elsewhere = new TrackingSnapshot(null, 0, null, 0, new PhysicalRect(3500, 820, 360, 200), Ticks(4.9));

        var anchor = controller.Next(Input(5.0, s_mouse, elsewhere));

        Assert.Equal(s_mouse, anchor);
    }

    [Fact]
    public void ToastFromBeforeStart_IsNoNews()
    {
        var controller = new AnchorController();

        var anchor = controller.Next(Input(5.0, s_mouse, Toast(4.9)));

        Assert.Equal(s_mouse, anchor);
    }

    private static AnchorController Started()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_mouse));
        return controller;
    }

    private static TrackingSnapshot Toast(double at) => new(null, 0, null, 0, s_toast, Ticks(at));

    private static AnchorInput Input(
        double seconds, PhysicalPoint mouse, TrackingSnapshot? tracking = null, TrackingOptions? options = null, double requestedAt = 0) =>
        new(Ticks(seconds), mouse, true, s_monitor, ZoomMode.Fullscreen, 4, LensSize.Default, DockLayout.Default,
            tracking ?? TrackingSnapshot.Empty, options ?? s_instant, Ticks(requestedAt));

    private static long Ticks(double seconds) => (long)(seconds * Stopwatch.Frequency);
}
