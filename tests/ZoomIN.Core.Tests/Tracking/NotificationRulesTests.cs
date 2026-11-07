using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

public sealed class NotificationRulesTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void SmallWindowInTheCorner_IsAToast() =>
        Assert.True(NotificationRules.LooksLikeToast(new PhysicalRect(1540, 820, 364, 200), s_monitor));

    [Fact]
    public void FullHeightFlyout_IsNoToast() =>
        Assert.False(NotificationRules.LooksLikeToast(new PhysicalRect(1500, 0, 400, 1032), s_monitor));

    [Fact]
    public void WideWindow_IsNoToast() =>
        Assert.False(NotificationRules.LooksLikeToast(new PhysicalRect(0, 0, 1920, 300), s_monitor));

    [Fact]
    public void EmptyWindow_IsNoToast() =>
        Assert.False(NotificationRules.LooksLikeToast(new PhysicalRect(1540, 820, 0, 0), s_monitor));

    [Fact]
    public void WindowOnOtherMonitor_IsNoToastOfThisMonitor() =>
        Assert.False(NotificationRules.LooksLikeToast(new PhysicalRect(3460, 820, 364, 200), s_monitor));

    [Fact]
    public void RequestNotification_StampsTheModel()
    {
        var model = new TrackingModel(TrackingOptions.Default);
        Assert.Equal(0, model.NotificationRequestedAt);

        model.RequestNotification();

        Assert.True(model.NotificationRequestedAt > 0);
    }
}
