using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

public sealed class WindowLayoutTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly WindowKind s_word = new("winword.exe", "OpusApp");
    private static readonly WindowKind s_terminal = new("windowsterminal.exe", "CASCADIA_HOSTING_WINDOW_CLASS");
    // The factory automatic filter (smart inversion since ADR-0024).
    private static readonly byte s_auto = WindowPalette.For(WindowFilterOptions.Default.AutoFilter);

    [Fact]
    public void OnlyDarkWindows_GiveNoRegions()
    {
        var regions = WindowLayout.Build([Window(1, 0, 0, 800, 600, s_terminal, WindowBrightness.Dark)], WindowFilterOptions.Default);

        Assert.Same(WindowRegions.Empty, regions);
    }

    [Fact]
    public void LightAndDarkSideBySide_OnlyTheLightOneIsFiltered()
    {
        var regions = WindowLayout.Build(
            [
                Window(1, 0, 0, 900, 1000, s_word, WindowBrightness.Light),
                Window(2, 1000, 0, 900, 1000, s_terminal, WindowBrightness.Dark),
            ],
            WindowFilterOptions.Default);

        Assert.Equal(1, regions.Count);
        Assert.Equal(new PhysicalRect(0, 0, 900, 1000), regions.Rects[0]);
        Assert.Equal(s_auto, regions.Palette[0]);
    }

    [Fact]
    public void DarkWindowAboveALightOne_IsKeptAsCover()
    {
        var regions = WindowLayout.Build(
            [
                Window(2, 100, 100, 400, 300, s_terminal, WindowBrightness.Dark),
                Window(1, 0, 0, 900, 1000, s_word, WindowBrightness.Light),
            ],
            WindowFilterOptions.Default);

        Assert.Equal(2, regions.Count);
        Assert.Equal(WindowPalette.Monitor, regions.Palette[0]);
        Assert.Equal(s_auto, regions.Palette[1]);
    }

    [Fact]
    public void FullyCoveredWindow_IsLeftOut()
    {
        var regions = WindowLayout.Build(
            [
                Window(2, 0, 0, 1920, 1080, s_terminal, WindowBrightness.Dark),
                Window(1, 100, 100, 400, 300, s_word, WindowBrightness.Light),
            ],
            WindowFilterOptions.Default);

        Assert.Same(WindowRegions.Empty, regions);
    }

    [Fact]
    public void Menu_ShowsWhatItsOwnerShows()
    {
        var regions = WindowLayout.Build(
            [
                Popup(3, owner: 1, processId: 10),
                Window(1, 0, 0, 900, 1000, s_word, WindowBrightness.Light, processId: 10),
            ],
            WindowFilterOptions.Default);

        Assert.Equal(s_auto, regions.Palette[0]);
    }

    [Fact]
    public void MenuWithoutOwner_TakesTheAppWindowBelow()
    {
        var regions = WindowLayout.Build(
            [
                Popup(3, owner: 0, processId: 10),
                Window(2, 1000, 0, 900, 1000, s_terminal, WindowBrightness.Dark, processId: 20),
                Window(1, 0, 0, 900, 1000, s_word, WindowBrightness.Light, processId: 10),
            ],
            WindowFilterOptions.Default);

        Assert.Equal(s_auto, regions.Palette[0]);
    }

    [Fact]
    public void MoreWindowsThanTheShaderTakes_KeepsTheFrontOnes()
    {
        var windows = Enumerable.Range(0, WindowRegions.Max + 8)
            .Select(i => Window(i + 1, i * 10, i * 10, 100, 100, s_word, WindowBrightness.Light))
            .ToList();

        var regions = WindowLayout.Build(windows, WindowFilterOptions.Default);

        Assert.Equal(WindowRegions.Max, regions.Count);
        Assert.Equal(windows[0].Bounds, regions.Rects[0]);
        Assert.Equal(8, regions.Dropped);
    }

    [Fact]
    public void WithinTheLimit_NothingIsDropped()
    {
        var regions = WindowLayout.Build([Window(1, 0, 0, 900, 1000, s_word, WindowBrightness.Light)], WindowFilterOptions.Default);

        Assert.Equal(0, regions.Dropped);
    }

    [Fact]
    public void Off_GivesNoRegions()
    {
        var regions = WindowLayout.Build(
            [Window(1, 0, 0, 900, 1000, s_word, WindowBrightness.Light)], WindowFilterOptions.Default with { Enabled = false });

        Assert.Same(WindowRegions.Empty, regions);
    }

    [Fact]
    public void InvertedMonitor_LightWindowIsNotInvertedTwice_DarkOneIsInverted()
    {
        var regions = WindowLayout.Build(
            [
                Window(1, 0, 0, 900, 1000, s_word, WindowBrightness.Light),
                Window(2, 1000, 0, 900, 1000, s_terminal, WindowBrightness.Dark),
            ],
            WindowFilterOptions.Default);
        var effects = new DisplayEffects(new FilterSettings(true, ColorFilter.Inverted, 1, 0), Smoothing.Text, PointerStyle.Default);
        var white = (1f, 1f, 1f);

        // The light window gets the inversion once (its own), not its own plus the monitor's.
        Assert.Equal((0f, 0f, 0f), effects.WindowMatrices[regions.Palette[0]].Apply(1, 1, 1));

        // The dark terminal is not a region: it shows the monitor filter and turns light.
        Assert.Equal(white, effects.Matrix.Apply(0, 0, 0));
    }

    [Fact]
    public void ChangesMonitor_OnlyWhenARegionLooksDifferent()
    {
        var regions = WindowLayout.Build([Window(1, 0, 0, 900, 1000, s_word, WindowBrightness.Light)], WindowFilterOptions.Default);
        var plain = DisplayEffects.Default;
        var smartMonitor = new DisplayEffects(new FilterSettings(true, ColorFilter.InvertedSmart, 1, 0), Smoothing.Text, PointerStyle.Default);
        var yellowMonitor = new DisplayEffects(new FilterSettings(true, ColorFilter.YellowOnBlack, 1, 0), Smoothing.Text, PointerStyle.Default);

        Assert.True(regions.ChangesMonitor(s_monitor, plain));
        Assert.False(regions.ChangesMonitor(new PhysicalRect(1920, 0, 1920, 1080), plain));
        Assert.False(regions.ChangesMonitor(s_monitor, smartMonitor));
        Assert.True(regions.ChangesMonitor(s_monitor, yellowMonitor));
    }

    private static TrackedWindow Window(
        nint handle, double x, double y, double width, double height, WindowKind kind, WindowBrightness brightness, int processId = 1) =>
        new(handle, new PhysicalRect(x, y, width, height), kind, WindowRole.Normal, brightness, 0, processId);

    private static TrackedWindow Popup(nint handle, nint owner, int processId) =>
        new(handle, new PhysicalRect(50, 50, 200, 300), new WindowKind("winword.exe", "#32768"), WindowRole.Popup,
            WindowBrightness.Pending, owner, processId);
}
