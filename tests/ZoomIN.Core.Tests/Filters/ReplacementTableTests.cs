using ZoomIN.Core.Filters;

namespace ZoomIN.Core.Tests.Filters;

/// <summary>Colour replacement (M18, C3) on the CPU reference that mirrors Magnify.hlsl.</summary>
public sealed class ReplacementTableTests
{
    private const float Tolerance = 0.002f;

    [Fact]
    public void Empty_WhenNoSchemeReplaces()
    {
        var table = ReplacementTable.Build([new CustomScheme { Id = FilterId.Custom1 }]);

        Assert.Same(ReplacementTable.Empty, table);
        Assert.False(table.Any);
        Assert.Equal(0, table.CountOf(0));
    }

    [Fact]
    public void Build_PlacesEntriesByPlace_ThreePerScheme()
    {
        var table = ReplacementTable.Build(
        [
            new CustomScheme { Id = FilterId.Custom3, Replacements = [Red(0), Red(0)] },
        ]);

        Assert.True(table.Any);
        Assert.Equal(2, table.CountOf(2));
        Assert.Equal(0, table.CountOf(0));
        Assert.Equal(0, table.CountOf(-1));
        Assert.Equal(0, table.CountOf(9));
        Assert.Equal(ReplacementTable.Size, table.Items.Length);
        Assert.NotEqual(default, table.Items[2 * ReplacementTable.PerScheme]);
        Assert.Equal(default, table.Items[0]);
    }

    [Fact]
    public void TheColorItself_BecomesExactlyTheNewOne()
    {
        var table = Table(Red(0));

        AssertColor((1, 1, 0), table.Apply(0, 1, 0, 0));
    }

    [Fact]
    public void ToleranceZero_LeavesNeighbourColoursUnchanged()
    {
        var table = Table(Red(0));

        // 16 steps less red, a little green: clearly a different colour.
        AssertColor((239 / 255f, 8 / 255f, 0), table.Apply(0, 239 / 255f, 8 / 255f, 0));
        AssertColor((1, 1, 1), table.Apply(0, 1, 1, 1));
        AssertColor((0, 0, 1), table.Apply(0, 0, 0, 1));
    }

    [Fact]
    public void HigherTolerance_CatchesNearColours_WithASoftEdge()
    {
        var table = Table(Red(30));

        var near = table.Apply(0, 239 / 255f, 8 / 255f, 0);
        var far = table.Apply(0, 0, 0, 1);

        Assert.True(near.G > 0.9f, $"near green {near.G}");
        AssertColor((0, 0, 1), far);
        Assert.Equal(0.01f, ReplacementTable.Radius(0), 0.0001f);
        Assert.True(ReplacementTable.Radius(30) > ReplacementTable.Radius(10));
    }

    [Fact]
    public void SeveralReplacements_AddUp_InAnyOrder()
    {
        var blueToGreen = new ColorReplacement { From = "#0000FF", To = "#00FF00", Tolerance = 0 };
        var a = Table(Red(0), blueToGreen);
        var b = Table(blueToGreen, Red(0));

        AssertColor((1, 1, 0), a.Apply(0, 1, 0, 0));
        AssertColor((0, 1, 0), a.Apply(0, 0, 0, 1));
        Assert.Equal(a.Apply(0, 0.5f, 0.2f, 0.9f), b.Apply(0, 0.5f, 0.2f, 0.9f));
    }

    [Fact]
    public void Ycc_OfGreyHasNoColourDifference()
    {
        var (y, cb, cr) = ReplacementTable.Ycc(0.5f, 0.5f, 0.5f);

        Assert.Equal(0.5f, y, 0.0001f);
        Assert.Equal(0, cb, 0.0001f);
        Assert.Equal(0, cr, 0.0001f);
    }

    private static ColorReplacement Red(int tolerance) => new() { From = "#FF0000", To = "#FFFF00", Tolerance = tolerance };

    private static ReplacementTable Table(params ColorReplacement[] replacements) =>
        ReplacementTable.Build([new CustomScheme { Id = FilterId.Custom1, Replacements = replacements }]);

    private static void AssertColor((float R, float G, float B) expected, (float R, float G, float B) actual)
    {
        Assert.Equal(expected.R, actual.R, Tolerance);
        Assert.Equal(expected.G, actual.G, Tolerance);
        Assert.Equal(expected.B, actual.B, Tolerance);
    }
}
