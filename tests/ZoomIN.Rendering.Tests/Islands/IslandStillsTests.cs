using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Islands;

namespace ZoomIN.Rendering.Tests.Islands;

/// <summary>Still images of islands (M19 B1): taken once, kept while a drag changes the source, freed when not needed.</summary>
public sealed class IslandStillsTests
{
    [Fact]
    public void Still_IsTakenOnce_AndFreedWhenLiveAgain()
    {
        using var rig = new IslandRig();
        using var stills = new IslandStills(rig.Gpu.Device, NullLogger.Instance);
        var still = IslandRig.Scene(null, IslandRig.At(40, 0, still: true)).Set;
        var id = still[0].Id;

        stills.Update(rig.Gpu.Context, rig.Image.Texture, still, IslandRig.Monitor, IslandRig.Bounds, dragging: false);
        var first = stills.For(id, still[0].Source);
        stills.Update(rig.Gpu.Context, rig.Image.Texture, still, IslandRig.Monitor, IslandRig.Bounds, dragging: false);

        Assert.NotNull(first);
        Assert.Same(first, stills.For(id, still[0].Source));

        var live = still.Replace(0, still[0] with { Still = false });
        stills.Update(rig.Gpu.Context, rig.Image.Texture, live, IslandRig.Monitor, IslandRig.Bounds, dragging: false);
        Assert.Null(stills.For(id, still[0].Source));
    }

    [Fact]
    public void SourceChangingDuringADrag_IsNotTakenAgain_UntilTheDragEnds()
    {
        using var rig = new IslandRig();
        using var stills = new IslandStills(rig.Gpu.Device, NullLogger.Instance);
        var set = IslandRig.Scene(null, IslandRig.At(40, 0, still: true)).Set;
        var id = set[0].Id;
        stills.Update(rig.Gpu.Context, rig.Image.Texture, set, IslandRig.Monitor, IslandRig.Bounds, dragging: false);
        var resized = set.Replace(0, set[0] with { Source = new PhysicalRect(40, 0, 20, 12) });

        stills.Update(rig.Gpu.Context, rig.Image.Texture, resized, IslandRig.Monitor, IslandRig.Bounds, dragging: true);

        Assert.Null(stills.For(id, resized[0].Source)); // drawn live during the drag
        Assert.NotNull(stills.For(id, set[0].Source)); // the old image is kept, not replaced per step

        stills.Update(rig.Gpu.Context, rig.Image.Texture, resized, IslandRig.Monitor, IslandRig.Bounds, dragging: false);
        Assert.Equal(20, stills.For(id, resized[0].Source)!.Width);
    }

    [Fact]
    public void LiveThenStillAgain_WhileNotDrawn_TakesANewImage()
    {
        using var rig = new IslandRig();
        using var red = Gpu.TestImage.Create(rig.Gpu.Device, IslandRig.Size, (_, _) => Gpu.TestImage.Red);
        var scene = IslandRig.Scene(null, IslandRig.At(40, 0, still: true));
        rig.Draw(scene); // blue taken
        Assert.Equal(Gpu.TestImage.Blue, rig.Draw(scene, image: red).At(20, 34)); // kept

        // Switched live and still again on the settings page (Take + 1) without a frame in between.
        var again = scene with { Set = scene.Set.Replace(0, scene.Set[0] with { Take = scene.Set[0].Take + 1 }) };

        Assert.Equal(Gpu.TestImage.Red, rig.Draw(again, image: red).At(20, 34));
    }

    [Fact]
    public void Blink_StopsAsking_ForRedraws_WhenTheEditModeEnds_EvenWithoutADraw()
    {
        using var rig = new IslandRig();
        var editing = IslandRig.Scene(new IslandEditView(-1, null, Stopwatch.GetTimestamp()), IslandRig.At(40, 0));
        rig.Draw(editing);

        rig.Layer.Update(null, Stopwatch.GetTimestamp());

        Assert.False(rig.Layer.NeedsRedraw);
    }
}
