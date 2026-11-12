using System.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Tests.Gpu;

namespace ZoomIN.Rendering.Tests.Islands;

/// <summary>
/// Islands on WARP (M19 B1): the right source part, filters as in the main picture, frame colour,
/// still image, edit marks. Picture of the island: (8, 24)–(40, 56); frame 3 px.
/// </summary>
public sealed class IslandLayerTests
{
    private static readonly Bgra s_transparent = new(0, 0, 0, 0);

    [Fact]
    public void Island_ShowsItsSourcePart_Magnified()
    {
        using var rig = new IslandRig();

        var blue = rig.Draw(IslandRig.Scene(null, IslandRig.At(40, 0)));
        var green = rig.Draw(IslandRig.Scene(null, IslandRig.At(0, 0)));

        Assert.Equal(TestImage.Blue, blue.At(20, 34));
        Assert.Equal(TestImage.Blue, blue.At(38, 54));
        Assert.Equal(TestImage.Green, green.At(20, 34));
        Assert.Equal(s_transparent, blue.At(50, 10));
        Assert.Equal(s_transparent, blue.At(2, 2));
    }

    [Fact]
    public void Island_HasAFrame_InTheLensFrameColour()
    {
        using var rig = new IslandRig();

        var pixels = rig.Draw(IslandRig.Scene(null, IslandRig.At(40, 0)));
        var frame = pixels.At(6, 34);

        Assert.InRange(frame.R, 254, 255);
        Assert.InRange(frame.G, 193, 195);
        Assert.InRange(frame.B, 7, 9);
        Assert.Equal(255, frame.A);
    }

    [Fact]
    public void ColourFilter_ActsInTheIsland()
    {
        using var rig = new IslandRig();

        var pixels = rig.Draw(IslandRig.Scene(null, IslandRig.At(40, 0)), IslandRig.Effects(FilterId.Inverted));

        // Blue inverted is yellow.
        Assert.Equal(new Bgra(0, 255, 255, 255), pixels.At(20, 34));
    }

    [Fact]
    public void StillIsland_KeepsItsImage_LiveIslandFollows()
    {
        using var rig = new IslandRig();
        using var red = TestImage.Create(rig.Gpu.Device, IslandRig.Size, (_, _) => TestImage.Red);
        var still = IslandRig.Scene(null, IslandRig.At(40, 0, still: true));
        var live = IslandRig.Scene(null, IslandRig.At(40, 0));

        rig.Draw(still);
        var later = rig.Draw(still, image: red);
        var liveLater = rig.Draw(live, image: red);

        Assert.Equal(TestImage.Blue, later.At(20, 34));
        Assert.Equal(TestImage.Blue, later.At(38, 54));
        Assert.Equal(TestImage.Red, liveLater.At(20, 34));
    }

    [Fact]
    public void StillIsland_WithAFilter_SamplesTheRightPartOfItsImage()
    {
        using var rig = new IslandRig();

        // Source (8, 8)–(24, 24): green up to 16, red after; the still image holds only this part.
        var pixels = rig.Draw(IslandRig.Scene(null, IslandRig.At(8, 8, still: true)), IslandRig.Effects(FilterId.Grayscale));

        var greenGrey = pixels.At(10, 26);
        var redGrey = pixels.At(36, 52);
        Assert.Equal(greenGrey.R, greenGrey.B);
        Assert.Equal(redGrey.R, redGrey.B);
        Assert.True(greenGrey.R > redGrey.R, "Green is lighter than red in grey.");
    }

    [Fact]
    public void IslandOutsideItsMonitor_IsHidden()
    {
        using var rig = new IslandRig();
        var outside = IslandRig.At(40, 0) with { TargetOrigin = new PhysicalPoint(50, 50) };

        var pixels = rig.Draw(IslandRig.Scene(null, outside));

        Assert.Equal(s_transparent, pixels.At(56, 56));
    }

    [Fact]
    public void IslandOnAnotherMonitor_IsNotDrawnHere()
    {
        using var rig = new IslandRig();

        var pixels = rig.Draw(IslandRig.Scene(null, IslandRig.At(40, 0) with { MonitorId = @"\\.\OTHER" }));

        Assert.Equal(s_transparent, pixels.At(20, 34));
    }

    [Fact]
    public void EditMode_ShowsHandles_AndTheRubberBand()
    {
        using var rig = new IslandRig();
        var edit = new IslandEditView(0, new PhysicalRect(40, 4, 20, 20), Stopwatch.GetTimestamp());

        var pixels = rig.Draw(IslandRig.Scene(edit, IslandRig.At(40, 0)));

        // Handle 10 px centred on the top-left corner (8, 24) of the picture: covers the frame there.
        var handle = pixels.At(4, 21);
        Assert.Equal(255, handle.A);
        Assert.InRange(handle.R, 254, 255);

        // The band: white dashes on a dark line along its edges, nothing inside.
        Assert.Equal(new Bgra(255, 255, 255, 255), pixels.At(41, 4));
        Assert.Equal(s_transparent, pixels.At(50, 14));
    }

    [Fact]
    public void EditMode_FramesBlink()
    {
        using var rig = new IslandRig();
        var halfBlink = (long)(IslandEditView.BlinkSeconds * 1.5 * Stopwatch.Frequency);
        var edit = new IslandEditView(-1, null, Stopwatch.GetTimestamp() - halfBlink);
        var scene = IslandRig.Scene(edit, IslandRig.At(40, 0));

        rig.Layer.Update(scene, Stopwatch.GetTimestamp());
        Assert.True(rig.Layer.NeedsRedraw);
        var pixels = rig.Draw(scene);

        Assert.Equal(new Bgra(255, 255, 255, 255), pixels.At(6, 34));
        Assert.False(rig.Layer.NeedsRedraw);
    }

    [Fact]
    public void Begin_WithoutIslands_FreesTheStillOfARemovedIsland_ButKeepsTheOthers()
    {
        using var rig = new IslandRig();
        var scene = IslandRig.Scene(null, IslandRig.At(8, 8, still: true));
        var still = scene.Set[0]; // with the id the set gave it
        rig.Draw(scene);
        Assert.NotNull(rig.Stills.For(still.Id, still.Source));

        // A frame without islands (e.g. hidden) keeps the still of an island that is still there...
        rig.Layer.Begin(null, scene.Set, IslandRig.Monitor, Stopwatch.GetTimestamp());
        Assert.NotNull(rig.Stills.For(still.Id, still.Source));

        // ...and frees it once the island is gone, also without another drawn frame.
        rig.Layer.Begin(null, IslandSet.Empty, IslandRig.Monitor, Stopwatch.GetTimestamp());
        Assert.Null(rig.Stills.For(still.Id, still.Source));
    }
}
