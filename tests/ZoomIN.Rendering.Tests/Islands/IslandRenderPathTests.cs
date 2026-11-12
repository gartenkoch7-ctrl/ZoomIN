using System.Diagnostics;
using System.Diagnostics.Metrics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Tests.Gpu;
using ZoomIN.Rendering.Tests.RenderLoop;

namespace ZoomIN.Rendering.Tests.Islands;

/// <summary>
/// The island draw in the render loop (M19 B1): four islands, one of them still, with the edit
/// marks, allocate nothing once warmed up (AGENTS.md 2.3); "Schärfe"/"Fett" read the still image
/// at the same places as the live desktop.
/// </summary>
[Collection(MeterTests.Name)]
public sealed class IslandRenderPathTests
{
    private const int Frames = 1_000;

    [Fact]
    public void FourIslands_WithStillImageAndEditMarks_DoNotAllocate()
    {
        using var rig = new IslandRig();
        var edit = new IslandEditView(1, new PhysicalRect(2, 2, 30, 12), Stopwatch.GetTimestamp());
        var scene = IslandRig.Scene(
            edit,
            Island(0, 0, 0, 2, still: true),
            Island(16, 0, 32, 2),
            Island(32, 16, 0, 32),
            Island(48, 32, 32, 32));
        var effects = new DisplayEffects(new FilterSettings(true, FilterId.Inverted, 1, 0), Smoothing.Text, PointerStyle.Default)
        {
            Enhancement = new TextEnhancement(0.6, 1),
        };

        // GPU timing runs only while someone listens (as with diagnostics or OTLP on).
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "zoomin.render.gpu_time")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.Start();

        void Draw()
        {
            rig.Layer.Update(scene, Stopwatch.GetTimestamp());
            rig.Layer.Draw(rig.Gpu.Context, rig.Image.View, rig.Image.Texture, rig.Target.View, IslandRig.Monitor, IslandRig.Bounds, scene, effects, null);
            rig.Gpu.Context.Flush();
        }

        Draw();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            Draw();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Enhancement_OnAStillIsland_MatchesTheLiveOne_InsideTheImage()
    {
        using var rig = new IslandRig();
        var effects = new DisplayEffects(FilterSettings.Default, Smoothing.Text, PointerStyle.Default)
        {
            Enhancement = new TextEnhancement(1, 1),
        };

        // Grey text-like strokes (colour counts as photo, where both are off): a black column and row in white.
        using var strokes = TestImage.Create(rig.Gpu.Device, IslandRig.Size, (x, y) =>
            x is 15 or 16 || y == 17 ? new Bgra(20, 20, 20, 255) : new Bgra(235, 235, 235, 255));
        var plain = rig.Draw(IslandRig.Scene(null, IslandRig.At(8, 8)), effects with { Enhancement = TextEnhancement.Off }, strokes);
        var live = rig.Draw(IslandRig.Scene(null, IslandRig.At(8, 8)), effects, strokes);
        var still = rig.Draw(IslandRig.Scene(null, IslandRig.At(8, 8, still: true)), effects, strokes);
        Assert.NotEqual(plain.At(22, 40), live.At(22, 40)); // the enhancement acts at all

        // Near its edges the still image has no neighbours (clamped); 3 source pixels in, all match.
        for (var y = 24 + 6; y < 56 - 6; y++)
        {
            for (var x = 8 + 6; x < 40 - 6; x++)
            {
                Assert.Equal(live.At(x, y), still.At(x, y));
            }
        }
    }

    private static Island Island(double sourceX, double sourceY, double x, double y, bool still = false) =>
        new(0, IslandRig.Monitor, new PhysicalRect(sourceX, sourceY, 8, 8), new PhysicalPoint(x, y), 2, still);
}
