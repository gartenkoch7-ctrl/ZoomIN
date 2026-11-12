using ZoomIN.Core.Filters;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

/// <summary>ADR-0024 decision 7: the old factory "Inverted" becomes the smart inversion, once.</summary>
public sealed class AutoFilterMigrationTests
{
    [Fact]
    public void OldSettings_WithTheOldFactoryValue_BecomeSmart()
    {
        // Written before the revision existed: missing in the JSON = 0.
        var old = new WindowFilterOptions { AutoFilter = FilterId.Inverted };

        var options = old.Normalized();

        Assert.Equal(FilterId.InvertedSmart, options.AutoFilter);
        Assert.Equal(WindowFilterOptions.CurrentAutoFilterRevision, options.AutoFilterRevision);
    }

    [Fact]
    public void OldSettings_WithAnOwnChoice_KeepIt()
    {
        var old = new WindowFilterOptions { AutoFilter = FilterId.Grayscale };

        Assert.Equal(FilterId.Grayscale, old.Normalized().AutoFilter);
    }

    [Fact]
    public void InvertedChosenAfterTheMigration_Stays()
    {
        var chosen = WindowFilterOptions.Default.Normalized() with { AutoFilter = FilterId.Inverted };

        Assert.Equal(FilterId.Inverted, chosen.Normalized().AutoFilter);
        Assert.Equal(FilterId.Inverted, chosen.Normalized().Normalized().AutoFilter);
    }

    [Fact]
    public void Model_TakesTheMigratedValue()
    {
        var model = new WindowFilterModel(new WindowFilterOptions { AutoFilter = FilterId.Inverted });

        Assert.Equal(FilterId.InvertedSmart, model.Options.AutoFilter);
    }
}
