using ZoomIN.Core.Filters;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

public sealed class WindowFilterResolverTests
{
    private static readonly WindowKind s_word = new("winword.exe", "OpusApp");
    private static readonly WindowKind s_vlc = new("vlc.exe", "Qt5QWindowIcon");
    private static readonly WindowKind s_taskbar = new("explorer.exe", "Shell_TrayWnd");
    private static readonly WindowKind s_files = new("explorer.exe", "CabinetWClass");

    [Fact]
    public void LightWindow_GetsTheAutomaticFilter_DarkOneTheMonitorFilter()
    {
        var options = WindowFilterOptions.Default with { AutoFilter = ColorFilter.YellowOnBlack };

        Assert.Equal(WindowPalette.For(ColorFilter.YellowOnBlack), Resolve(s_word, WindowBrightness.Light, options));
        Assert.Equal(WindowPalette.Monitor, Resolve(s_word, WindowBrightness.Dark, options));
        Assert.Equal(WindowPalette.Monitor, Resolve(s_word, WindowBrightness.Pending, options));
        Assert.Equal(WindowPalette.Monitor, Resolve(s_word, WindowBrightness.Unknown, options));
    }

    [Fact]
    public void UserChoice_WinsOverTheMeasurement()
    {
        var none = WindowFilterOptions.Default.WithRule(WindowFilterRule.For(s_word, WindowFilterChoice.None));
        var gray = WindowFilterOptions.Default.WithRule(WindowFilterRule.For(s_word, WindowFilterChoice.Filter, ColorFilter.Grayscale));

        Assert.Equal(WindowPalette.Original, Resolve(s_word, WindowBrightness.Light, none));
        Assert.Equal(WindowPalette.For(ColorFilter.Grayscale), Resolve(s_word, WindowBrightness.Dark, gray));
    }

    [Fact]
    public void MediaApps_StartWithNone_ButAUserRuleWins()
    {
        Assert.Equal(WindowPalette.Original, Resolve(s_vlc, WindowBrightness.Light, WindowFilterOptions.Default));
        Assert.Equal(WindowPalette.Original, Resolve(new WindowKind("gimp-2.10.exe", "gdkWindowToplevel"), WindowBrightness.Light, WindowFilterOptions.Default));

        var automatic = WindowFilterOptions.Default.WithRule(WindowFilterRule.For(s_vlc, WindowFilterChoice.Automatic));
        Assert.Equal(WindowPalette.For(ColorFilter.Inverted), Resolve(s_vlc, WindowBrightness.Light, automatic));
    }

    [Fact]
    public void WindowsInterface_IsNeverFilteredOnItsOwn_EvenWithARule()
    {
        var rule = WindowFilterOptions.Default.WithRule(WindowFilterRule.For(s_taskbar, WindowFilterChoice.Filter));

        Assert.Equal(WindowPalette.Monitor, WindowFilterResolver.Resolve(s_taskbar, WindowRole.Shell, WindowBrightness.Light, rule));
        Assert.False(WindowFilterResolver.NeedsMeasurement(s_taskbar, WindowRole.Shell, rule));
    }

    [Fact]
    public void ExplorerFileWindows_AreApps_NotTheInterface()
    {
        Assert.False(WindowFilterPresets.IsShell(s_files));
        Assert.True(WindowFilterPresets.IsShell(s_taskbar));
        Assert.True(WindowFilterPresets.IsShell(new WindowKind("SearchHost.exe", "Windows.UI.Core.CoreWindow")));
    }

    [Fact]
    public void Off_LeavesEverythingToTheMonitorFilter()
    {
        var off = WindowFilterOptions.Default with { Enabled = false };

        Assert.Equal(WindowPalette.Monitor, Resolve(s_word, WindowBrightness.Light, off));
        Assert.False(WindowFilterResolver.NeedsMeasurement(s_word, WindowRole.Normal, off));
    }

    [Fact]
    public void OnlyAutomaticNormalWindows_AreMeasured()
    {
        var options = WindowFilterOptions.Default.WithRule(WindowFilterRule.For(s_files, WindowFilterChoice.None));

        Assert.True(WindowFilterResolver.NeedsMeasurement(s_word, WindowRole.Normal, options));
        Assert.False(WindowFilterResolver.NeedsMeasurement(s_word, WindowRole.Popup, options));
        Assert.False(WindowFilterResolver.NeedsMeasurement(s_files, WindowRole.Normal, options));
        Assert.False(WindowFilterResolver.NeedsMeasurement(s_vlc, WindowRole.Normal, options));
    }

    [Theory]
    [InlineData("#32768", false, false, false, WindowRole.Popup)]
    [InlineData("tooltips_class32", false, false, false, WindowRole.Popup)]
    [InlineData("Chrome_WidgetWin_2", true, true, false, WindowRole.Popup)]
    [InlineData("Chrome_WidgetWin_1", true, false, false, WindowRole.Normal)]
    [InlineData("#32770", false, true, false, WindowRole.Normal)]
    public void Roles_FollowClassAndStyle(string windowClass, bool popupStyle, bool owner, bool tool, WindowRole expected)
    {
        Assert.Equal(expected, WindowClasses.RoleOf(new WindowKind("app.exe", windowClass), popupStyle, owner, tool));
    }

    private static byte Resolve(WindowKind kind, WindowBrightness brightness, WindowFilterOptions options) =>
        WindowFilterResolver.Resolve(kind, WindowRole.Normal, brightness, options);
}
