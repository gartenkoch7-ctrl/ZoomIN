using ZoomIN.Core.Filters;

namespace ZoomIN.Core.Tests.Filters;

/// <summary>Places, lookup, order and clean-up of the own schemes (M18).</summary>
public sealed class CustomSchemesTests
{
    [Fact]
    public void Places_MapToFilterIdsAfterTheBuiltInOnes()
    {
        Assert.Equal(FilterId.Custom1, CustomSchemes.IdOf(0));
        Assert.Equal(FilterId.Custom4, CustomSchemes.IdOf(3));
        Assert.Equal(3, CustomSchemes.SlotOf(FilterId.Custom4));
        Assert.Equal(-1, CustomSchemes.SlotOf(FilterId.InvertedSmart));
        Assert.False(CustomSchemes.IsCustom(FilterId.WhiteOnBlue));
        Assert.True(CustomSchemes.IsCustom(FilterId.Custom2));
    }

    [Fact]
    public void FreeSlot_IsTheFirstGap_AndNullWhenFull()
    {
        IReadOnlyList<CustomScheme> some = [Scheme(FilterId.Custom1), Scheme(FilterId.Custom3)];
        IReadOnlyList<CustomScheme> full = [.. Enumerable.Range(0, CustomSchemes.Max).Select(i => Scheme(CustomSchemes.IdOf(i)))];

        Assert.Equal(FilterId.Custom1, CustomSchemes.FreeSlot([]));
        Assert.Equal(FilterId.Custom2, CustomSchemes.FreeSlot(some));
        Assert.Null(CustomSchemes.FreeSlot(full));
    }

    [Fact]
    public void Resolve_MissingSchemeOrUnknownValue_FallsBackToInverted()
    {
        IReadOnlyList<CustomScheme> schemes = [Scheme(FilterId.Custom2)];

        Assert.Equal(FilterId.Custom2, CustomSchemes.Resolve(FilterId.Custom2, schemes));
        Assert.Equal(FilterId.Inverted, CustomSchemes.Resolve(FilterId.Custom3, schemes));
        Assert.Equal(FilterId.Inverted, CustomSchemes.Resolve((FilterId)77, schemes));
        Assert.Equal(FilterId.Grayscale, CustomSchemes.Resolve(FilterId.Grayscale, schemes));
    }

    [Fact]
    public void Normalized_DropsBrokenEntries_SortsByPlace_AndBoundsValues()
    {
        var schemes = CustomSchemes.Normalized(
        [
            Scheme(FilterId.Custom3) with
            {
                Name = "  " + new string('x', 60) + " ",
                Color1 = "nonsense",
                Hue = "#abcdef",
                Strength = 250,
                Deficiency = (ColorDeficiency)9,
                Replacements =
                [
                    new ColorReplacement { Tolerance = -5 },
                    null!,
                    new ColorReplacement { From = "#00ff00", Tolerance = 500 },
                    new ColorReplacement(),
                    new ColorReplacement(),
                ],
            },
            Scheme(FilterId.Custom3),
            Scheme(FilterId.Grayscale),
            Scheme(FilterId.Custom1) with { Kind = (SchemeKind)12 },
            Scheme(FilterId.Custom2) with { Name = null!, Replacements = null! },
            null!,
        ]);

        Assert.Equal([FilterId.Custom1, FilterId.Custom2, FilterId.Custom3], schemes.Select(s => s.Id));
        Assert.Equal(SchemeKind.ReplaceOnly, schemes[0].Kind); // unknown kind: kept, only replaces
        Assert.Equal(string.Empty, schemes[1].Name);
        Assert.Empty(schemes[1].Replacements);

        var third = schemes[2];
        Assert.Equal(CustomScheme.MaxNameLength, third.Name.Length);
        Assert.Equal(new CustomScheme().Color1, third.Color1);
        Assert.Equal("#ABCDEF", third.Hue);
        Assert.Equal(CustomScheme.MaxStrength, third.Strength);
        Assert.Equal(ColorDeficiency.Deutan, third.Deficiency);
        Assert.Equal(CustomScheme.MaxReplacements, third.Replacements.Count);
        Assert.Equal(0, third.Replacements[0].Tolerance);
        Assert.Equal("#00FF00", third.Replacements[1].From);
        Assert.Equal(ColorReplacement.MaxTolerance, third.Replacements[1].Tolerance);
    }

    [Fact]
    public void LongName_IsNotCutInsideASurrogatePair()
    {
        var name = new string('x', CustomScheme.MaxNameLength - 1) + "😀";

        var cut = CustomSchemes.NormalizedScheme(Scheme(FilterId.Custom1) with { Name = name }).Name;

        Assert.Equal(new string('x', CustomScheme.MaxNameLength - 1), cut);
    }

    [Fact]
    public void Normalized_OfNull_IsEmpty()
    {
        Assert.Empty(CustomSchemes.Normalized(null));
    }

    [Fact]
    public void Order_IsBuiltInThenSchemesByPlace()
    {
        var order = ColorFilters.Order(CustomSchemes.Normalized([Scheme(FilterId.Custom4), Scheme(FilterId.Custom2)]));

        Assert.Equal([.. ColorFilters.DisplayOrder, FilterId.Custom2, FilterId.Custom4], order);
        Assert.Same(ColorFilters.DisplayOrder, ColorFilters.Order([]));
        Assert.Equal(9, ColorFilters.IndexIn(order, FilterId.Custom4));
        Assert.Equal(0, ColorFilters.IndexIn(order, FilterId.Custom1));
    }

    private static CustomScheme Scheme(FilterId id) => new() { Id = id, Name = id.ToString() };
}
