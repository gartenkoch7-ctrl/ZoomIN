using ZoomIN.Core.Filters;
using ZoomIN.Core.Sharing;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Sharing;

public sealed class ShareModelTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect s_window = new(100, 100, 800, 600);
    private static readonly SharedArea s_screenShare = new(ShareKind.Screen, s_monitor);

    [Fact]
    public void NothingShared_HasNoArea()
    {
        var model = new ShareModel(ShareOptions.Default);

        Assert.Null(model.Current.Area);
    }

    [Fact]
    public void Detected_ShowsArea_AndRaisesChanged()
    {
        var model = new ShareModel(ShareOptions.Default);
        var raised = 0;
        model.Changed += () => raised++;

        model.PublishDetected(s_screenShare);
        model.PublishDetected(s_screenShare); // same area again: no event

        Assert.Equal(s_monitor, model.Current.Area);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Detected_IgnoredWithoutAutoDetect()
    {
        var model = new ShareModel(ShareOptions.Default with { AutoDetect = false });

        model.PublishDetected(s_screenShare);

        Assert.Null(model.Current.Area);
    }

    [Fact]
    public void Disabled_IgnoresDetectionAndHotkeys()
    {
        var model = new ShareModel(ShareOptions.Default with { Enabled = false });

        model.PublishDetected(s_screenShare);
        model.ToggleWindow(42, s_window);

        Assert.Null(model.Current.Area);
        Assert.False(model.Current.HasManual);
    }

    [Fact]
    public void ToggleScreen_MarksMonitor_SecondPressClears()
    {
        var model = new ShareModel(ShareOptions.Default);

        model.ToggleScreen(s_monitor);
        Assert.Equal(s_monitor, model.Current.Area);

        model.ToggleScreen(s_monitor);
        Assert.Null(model.Current.Area);
    }

    [Fact]
    public void ToggleWindow_FollowsWindow_UntilClosed()
    {
        var model = new ShareModel(ShareOptions.Default);
        var moved = s_window with { X = 400 };

        model.ToggleWindow(42, s_window);
        model.PublishManualWindow(42, moved);
        Assert.Equal(moved, model.Current.Area);

        model.PublishManualWindow(42, null);
        Assert.Null(model.Current.Area);
        Assert.False(model.Current.HasManual);
    }

    [Fact]
    public void PublishManualWindow_ForOtherWindow_IsIgnored()
    {
        var model = new ShareModel(ShareOptions.Default);
        model.ToggleWindow(42, s_window);

        model.PublishManualWindow(7, null);

        Assert.Equal(s_window, model.Current.Area);
    }

    [Fact]
    public void Manual_WinsOverDetection()
    {
        var model = new ShareModel(ShareOptions.Default with { AutoDetect = false });
        model.ToggleWindow(42, s_window);

        model.SetOptions(ShareOptions.Default);
        model.PublishDetected(s_screenShare);

        Assert.Equal(s_window, model.Current.Area);
    }

    [Fact]
    public void Hotkey_HidesDetection_UntilShareEnds()
    {
        var model = new ShareModel(ShareOptions.Default);
        model.PublishDetected(s_screenShare);

        model.ToggleWindow(42, s_window);
        Assert.Null(model.Current.Area);
        Assert.False(model.Current.HasManual);

        model.PublishDetected(s_screenShare);
        Assert.Null(model.Current.Area);

        model.PublishDetected(null);
        model.PublishDetected(s_screenShare);
        Assert.Equal(s_monitor, model.Current.Area);
    }

    [Fact]
    public void Hotkey_AfterHiding_MarksAgain()
    {
        var model = new ShareModel(ShareOptions.Default);
        model.PublishDetected(s_screenShare);
        model.ToggleWindow(42, s_window); // hides the detected share

        model.ToggleWindow(42, s_window);

        Assert.Equal(s_window, model.Current.Area);
    }

    [Fact]
    public void Hotkey_WithManualAndDetection_ClearsBoth()
    {
        var model = new ShareModel(ShareOptions.Default with { AutoDetect = false });
        model.ToggleScreen(s_monitor);
        model.SetOptions(ShareOptions.Default);
        model.PublishDetected(new SharedArea(ShareKind.Window, s_window));

        model.ToggleScreen(s_monitor);

        Assert.Null(model.Current.Area);
    }

    [Fact]
    public void Matrix_FollowsChosenFilter()
    {
        var model = new ShareModel(ShareOptions.Default);

        model.SetOptions(ShareOptions.Default with { Filter = ColorFilter.WhiteOnBlue });

        Assert.Equal(ColorFilters.For(ColorFilter.WhiteOnBlue), model.Current.Matrix);
    }
}
