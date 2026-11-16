using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.HotPath;

/// <summary>
/// Own schemes on the render threads (AGENTS.md 2.3, M18): matrices, palette and replacements are
/// built when settings change; what a frame reads from the snapshot allocates nothing.
/// </summary>
public sealed class SchemeAllocationTests
{
    private const int Frames = 10_000;
    private const string Monitor = @"\\.\DISPLAY1";

    [Fact]
    public void ReadingSchemeEffects_DoesNotAllocate()
    {
        var schemes = Enumerable.Range(0, CustomSchemes.Max).Select(i => new CustomScheme
        {
            Id = CustomSchemes.IdOf(i),
            Kind = (SchemeKind)(i % 4),
            Replacements = [new ColorReplacement(), new ColorReplacement { From = "#0000FF" }, new ColorReplacement { From = "#00FF00" }],
        }).ToList();
        var model = new EffectsModel(new DisplayEffects(new FilterSettings(true, FilterId.Custom2, 1, 0), Smoothing.Text, PointerStyle.Default)
        {
            Schemes = schemes,
        });
        model.SetMonitorFilter(Monitor, new MonitorFilter(true, FilterId.Custom4));
        var rects = new PhysicalRect[WindowRegions.Max];
        var palette = new byte[WindowRegions.Max];
        for (var i = 0; i < rects.Length; i++)
        {
            rects[i] = new PhysicalRect(i * 20, i * 10, 300, 200);
            palette[i] = (byte)(WindowPalette.FirstScheme + (i % CustomSchemes.Max));
        }

        var regions = new WindowRegions(rects, palette);
        var screen = new PhysicalRect(5000, 0, 1920, 1080); // no region on it: every rectangle is checked
        var sum = Read(model, regions, screen); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            sum += Read(model, regions, screen);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    private static int Read(EffectsModel model, WindowRegions regions, PhysicalRect screen)
    {
        var effects = model.CurrentFor(Monitor);
        var sum = regions.ChangesMonitor(screen, effects) ? 1 : 0;
        sum += effects.ReplaceSlotOf(WindowPalette.Monitor) + effects.Replacements.CountOf(1) + effects.WindowMatrices.Length;
        foreach (var item in effects.Replacements.Items)
        {
            sum += item.Radius > 0 ? 1 : 0;
        }

        return sum;
    }
}
