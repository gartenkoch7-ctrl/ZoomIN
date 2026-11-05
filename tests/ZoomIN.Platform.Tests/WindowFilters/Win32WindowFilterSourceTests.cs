using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Platform.Monitors;
using ZoomIN.Platform.WindowFilters;

namespace ZoomIN.Platform.Tests.WindowFilters;

/// <summary>The tracker thread with its event hooks and look timer, end to end with the test's own windows.</summary>
[Collection(OnScreenTests.Name)]
public sealed class Win32WindowFilterSourceTests
{
    [Fact]
    public void NewLightWindow_BecomesARegion_AndClosingItRemovesTheRegion()
    {
        var bounds = new PhysicalRect(220, 260, 280, 240);
        var model = new WindowFilterModel(WindowFilterOptions.Default);
        using var source = new Win32WindowFilterSource(NullLogger<Win32WindowFilterSource>.Instance, new Win32MonitorProvider());
        source.Start(model);

        var white = TestWindow.White(bounds);
        try
        {
            WindowTrackerTests.SkipIfScreenCannotBeCopied();

            // Shown → event → update → looks over about a second → region.
            Assert.True(WaitFor(() => model.Regions.Rects.ToArray().Contains(bounds), TimeSpan.FromSeconds(4)), "no region for the light window");
            Assert.Contains(source.OpenKinds(), kind => kind.WindowClass == TestWindow.WhiteClass);
        }
        finally
        {
            white.Dispose();
        }

        Assert.True(WaitFor(() => !model.Regions.Rects.ToArray().Contains(bounds), TimeSpan.FromSeconds(2)), "region stayed after closing");
    }

    [Fact]
    public void StartAndDispose_WithoutWindows_DoesNotThrow()
    {
        var model = new WindowFilterModel(WindowFilterOptions.Default with { Enabled = false });
        var source = new Win32WindowFilterSource(NullLogger<Win32WindowFilterSource>.Instance, new Win32MonitorProvider());

        var exception = Record.Exception(() =>
        {
            source.Start(model);
            model.SetOptions(WindowFilterOptions.Default);
            Thread.Sleep(100);
            source.Dispose();
        });

        Assert.Null(exception);
    }

    private static bool WaitFor(Func<bool> condition, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return condition();
    }
}
