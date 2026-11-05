using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.HotPath;

/// <summary>Render threads read the window regions every frame (AGENTS.md 2.3): 0 B.</summary>
public sealed class WindowRegionsAllocationTests
{
    private const int Frames = 10_000;

    [Fact]
    public void ReadingRegions_DoesNotAllocate()
    {
        var model = new WindowFilterModel(WindowFilterOptions.Default);
        var rects = new PhysicalRect[WindowRegions.Max];
        var palette = new byte[WindowRegions.Max];
        for (var i = 0; i < rects.Length; i++)
        {
            rects[i] = new PhysicalRect(i * 20, i * 10, 300, 200);
            palette[i] = i % 3 == 0 ? WindowPalette.Monitor : WindowPalette.For(ColorFilter.Inverted);
        }

        model.Publish(new WindowRegions(rects, palette));
        var effects = new DisplayEffects(new FilterSettings(true, ColorFilter.YellowOnBlack, 1.2, 0), Smoothing.Text, PointerStyle.Default);
        var monitor = new PhysicalRect(5000, 0, 1920, 1080); // no region on it: every rectangle is checked
        var hits = Read(model, monitor, effects); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            hits += Read(model, monitor, effects);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, hits);
    }

    private static int Read(WindowFilterModel model, PhysicalRect monitor, DisplayEffects effects)
    {
        var regions = model.Regions;
        var sum = regions.ChangesMonitor(monitor, effects) ? 1 : 0;
        foreach (var rect in regions.Rects)
        {
            sum += RectMath.Intersects(rect, monitor) ? 1 : 0;
        }

        return sum;
    }
}
