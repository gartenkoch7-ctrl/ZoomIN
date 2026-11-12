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

        stills.Update(rig.Gpu.Context, rig.Image.Texture, still, IslandRig.Monitor, IslandRig.Bounds, editing: false);
        var first = stills.For(id, still[0].Source);
        stills.Update(rig.Gpu.Context, rig.Image.Texture, still, IslandRig.Monitor, IslandRig.Bounds, editing: false);

        Assert.NotNull(first);
        Assert.Same(first, stills.For(id, still[0].Source));

        var live = still.Replace(0, still[0] with { Still = false });
        stills.Update(rig.Gpu.Context, rig.Image.Texture, live, IslandRig.Monitor, IslandRig.Bounds, editing: false);
        Assert.Null(stills.For(id, still[0].Source));
    }

    [Fact]
    public void SourceChangingWhileEditing_IsNotTakenAgain_UntilTheEditModeEnds()
    {
        using var rig = new IslandRig();
        using var stills = new IslandStills(rig.Gpu.Device, NullLogger.Instance);
        var set = IslandRig.Scene(null, IslandRig.At(40, 0, still: true)).Set;
        var id = set[0].Id;
        stills.Update(rig.Gpu.Context, rig.Image.Texture, set, IslandRig.Monitor, IslandRig.Bounds, editing: false);
        var resized = set.Replace(0, set[0] with { Source = new PhysicalRect(40, 0, 20, 12) });

        stills.Update(rig.Gpu.Context, rig.Image.Texture, resized, IslandRig.Monitor, IslandRig.Bounds, editing: true);

        Assert.Null(stills.For(id, resized[0].Source)); // drawn live during the drag
        Assert.NotNull(stills.For(id, set[0].Source)); // the old image is kept, not replaced per step

        stills.Update(rig.Gpu.Context, rig.Image.Texture, resized, IslandRig.Monitor, IslandRig.Bounds, editing: false);
        Assert.Equal(20, stills.For(id, resized[0].Source)!.Width);
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
