using Windows.Win32.Foundation;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.WindowFilters;

namespace ZoomIN.Platform.Tests.WindowFilters;

/// <summary>
/// The title bar against real windows: read from DWM/the client area, and counted apart from the
/// page in one look. Only the test's own windows are looked at.
/// </summary>
[Collection(OnScreenTests.Name)]
public sealed class TitleBarLookTests
{
    private static readonly PhysicalRect s_page = new(200, 200, 300, 300);
    private static readonly PhysicalRect s_strip = new(200, 160, 300, 40);
    private static readonly PhysicalRect s_frame = new(200, 160, 300, 340); // strip + page, like one window

    [Fact]
    public void WindowWithSystemTitleBar_HasATitleBarHeight_PopupHasNone()
    {
        using var captioned = TestWindow.WhiteWithTitleBar(new PhysicalRect(600, 200, 400, 300));
        using var popup = TestWindow.White(s_page);
        Assert.True(WindowEnumerator.TryGetBounds((HWND)captioned.Handle, out var bounds));

        var height = TitleBarReader.HeightOf((HWND)captioned.Handle, bounds);

        Assert.InRange(height, 15, (int)bounds.Height / 3);
        Assert.True(WindowEnumerator.TryGetBounds((HWND)popup.Handle, out var popupBounds));
        Assert.Equal(0, TitleBarReader.HeightOf((HWND)popup.Handle, popupBounds));
    }

    [Fact]
    public void Look_CountsADarkTitleBarApart_FromALightPage()
    {
        using var page = TestWindow.White(s_page);
        using var strip = TestWindow.Black(s_strip);
        WindowTrackerTests.SkipIfScreenCannotBeCopied();
        using var sampler = new ScreenSampler();

        var request = new LookRequest(page.Handle, s_frame, [], [new PhysicalRect(0, 0, 4000, 4000)], 60, TitleBarBottom: s_strip.Bottom);
        var result = request.Take(sampler);

        Assert.True(result.IsLight);
        Assert.False(result.TitleBarIsLight);
    }

    [Fact]
    public void Look_WithoutTitleBar_GivesNoTitleBarResult()
    {
        using var page = TestWindow.White(s_page);
        WindowTrackerTests.SkipIfScreenCannotBeCopied();
        using var sampler = new ScreenSampler();

        var result = new LookRequest(page.Handle, s_page, [], [new PhysicalRect(0, 0, 4000, 4000)], 60).Take(sampler);

        Assert.True(result.IsLight);
        Assert.Null(result.TitleBarIsLight);
    }
}
