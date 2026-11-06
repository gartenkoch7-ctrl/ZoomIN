using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Platform.Monitors;
using ZoomIN.Platform.WindowFilters;

namespace ZoomIN.Platform.Tests.WindowFilters;

/// <summary>
/// The tracker against real windows – only the test's own solid windows are looked at or
/// changed. Needs an unlocked desktop: when the screen cannot be copied, the looks are skipped.
/// </summary>
[Collection(OnScreenTests.Name)]
public sealed class WindowTrackerTests
{
    private static readonly PhysicalRect s_whiteBounds = new(200, 200, 300, 300);
    private static readonly PhysicalRect s_blackBounds = new(600, 200, 300, 300);
    // The factory automatic filter (smart inversion since ADR-0024).
    private static readonly byte s_inverted = WindowPalette.For(WindowFilterOptions.Default.AutoFilter);

    [Fact]
    public void Enumerator_ListsTheTestWindows_FrontToBack_WithTheirBounds()
    {
        using var white = TestWindow.White(s_whiteBounds);
        using var black = TestWindow.Black(s_blackBounds);

        var windows = new List<RawWindow>();
        WindowEnumerator.Collect(windows);

        var whiteAt = windows.FindIndex(w => w.Handle == white.Handle);
        var blackAt = windows.FindIndex(w => w.Handle == black.Handle);
        Assert.True(whiteAt >= 0 && blackAt >= 0, "both test windows must be listed");
        Assert.True(blackAt < whiteAt, "the window created last lies in front");
        Assert.Equal(s_whiteBounds, windows[whiteAt].Bounds);
        Assert.Equal((uint)Environment.ProcessId, windows[whiteAt].ProcessId);
    }

    [Fact]
    public void LightWindow_IsMeasuredOnce_AndBecomesTheOnlyRegion()
    {
        using var white = TestWindow.White(s_whiteBounds);
        using var black = TestWindow.Black(s_blackBounds);
        SkipIfScreenCannotBeCopied();
        var model = new WindowFilterModel(WindowFilterOptions.Default);
        using var tracker = NewTracker(model);

        // Other open windows are measured too (looks only read the screen); the test windows are
        // fully visible, so two equal looks decide them after about 0.6 s (schedule 250/600 ms).
        var started = Stopwatch.GetTimestamp();
        tracker.Update();
        Assert.Equal(WindowPalette.Monitor, PaletteOf(tracker, white.Handle)); // not decided yet
        while (PaletteOf(tracker, white.Handle) == WindowPalette.Monitor && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(3))
        {
            Thread.Sleep(20);
            if (tracker.Look())
            {
                tracker.Update();
            }
        }

        Assert.InRange(Stopwatch.GetElapsedTime(started), TimeSpan.FromMilliseconds(550), TimeSpan.FromSeconds(1.2));
        Settle(tracker);
        Assert.Equal(s_inverted, PaletteOf(tracker, white.Handle));
        Assert.Equal(WindowPalette.Monitor, PaletteOf(tracker, black.Handle));
        var regions = model.Regions;
        Assert.Contains(s_whiteBounds, regions.Rects.ToArray());
        Assert.DoesNotContain(s_blackBounds, regions.Rects.ToArray());
    }

    [Fact]
    public void OwnedPopup_ShowsWhatItsOwnerShows_WithoutLooks()
    {
        using var white = TestWindow.White(s_whiteBounds);
        SkipIfScreenCannotBeCopied();
        var model = new WindowFilterModel(WindowFilterOptions.Default);
        using var tracker = NewTracker(model);
        Settle(tracker);

        // A black owned popup (like a menu) over the light window: inverted like its owner.
        using var menu = TestWindow.Black(new PhysicalRect(250, 250, 120, 160), white.Handle);
        tracker.Update();

        Assert.Equal(s_inverted, PaletteOf(tracker, menu.Handle));
    }

    [Fact]
    public void HiddenAndShownAgain_KeepsItsResult_WithoutNewLooks()
    {
        using var white = TestWindow.White(s_whiteBounds);
        SkipIfScreenCannotBeCopied();
        var model = new WindowFilterModel(WindowFilterOptions.Default);
        using var tracker = NewTracker(model);
        Settle(tracker);

        white.Hide();
        tracker.Update();
        Assert.DoesNotContain(s_whiteBounds, model.Regions.Rects.ToArray());

        white.Show();
        tracker.Update();
        Assert.Equal(s_inverted, PaletteOf(tracker, white.Handle));
    }

    [Fact]
    public void UserChoice_NeedsNoLooks_AndOffPublishesNothing()
    {
        using var white = TestWindow.White(s_whiteBounds);
        var kind = new WindowKind(Process.GetCurrentProcess().ProcessName.ToLowerInvariant() + ".exe", TestWindow.WhiteClass);
        var options = WindowFilterOptions.Default.WithRule(WindowFilterRule.For(kind, WindowFilterChoice.Filter, ColorFilter.Grayscale));
        var model = new WindowFilterModel(options);
        using var tracker = NewTracker(model);

        tracker.Update();
        Assert.Equal(WindowPalette.For(ColorFilter.Grayscale), PaletteOf(tracker, white.Handle));

        model.SetOptions(options with { Enabled = false });
        tracker.Update();
        Assert.Same(WindowRegions.Empty, model.Regions);
    }

    internal static WindowTracker NewTracker(WindowFilterModel model) =>
        new(model, new Win32MonitorProvider(), new WindowFilterTelemetry(NullLogger.Instance), new ScreenSampler());

    // Lets the looks run as the timer would, until every window is decided.
    internal static TimeSpan Settle(WindowTracker tracker)
    {
        var started = Stopwatch.GetTimestamp();
        tracker.Update();
        while (tracker.HasPendingLooks && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(4))
        {
            Thread.Sleep(50);
            if (tracker.Look())
            {
                tracker.Update();
            }
        }

        return Stopwatch.GetElapsedTime(started);
    }

    // Locked screen, disconnected session or display off: GDI hands out black.
    internal static void SkipIfScreenCannotBeCopied()
    {
        Thread.Sleep(150); // let the test window paint
        using var sampler = new ScreenSampler();
        var copied = sampler.Capture(s_whiteBounds);
        var light = copied && BrightnessMeter.Luma(sampler.Pixels[ScreenSampler.Size * 32 + 32]) >= BrightnessMeter.LightLuma;
        Assert.SkipUnless(light, "The screen cannot be copied (locked or no display); looks are not testable here.");
    }

    private static byte PaletteOf(WindowTracker tracker, nint handle)
    {
        var snapshot = tracker.Snapshot;
        var index = Array.FindIndex(snapshot.Windows, w => w.Handle == handle);
        Assert.True(index >= 0, "window not tracked");
        return snapshot.Palette[index];
    }
}
