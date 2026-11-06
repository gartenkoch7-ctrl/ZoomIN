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
        var old = new WindowFilterOptions { AutoFilter = ColorFilter.Inverted };

        var options = old.Normalized();

        Assert.Equal(ColorFilter.InvertedSmart, options.AutoFilter);
        Assert.Equal(WindowFilterOptions.CurrentAutoFilterRevision, options.AutoFilterRevision);
    }

    [Fact]
    public void OldSettings_WithAnOwnChoice_KeepIt()
    {
        var old = new WindowFilterOptions { AutoFilter = ColorFilter.Grayscale };

        Assert.Equal(ColorFilter.Grayscale, old.Normalized().AutoFilter);
    }

    [Fact]
    public void InvertedChosenAfterTheMigration_Stays()
    {
        var chosen = WindowFilterOptions.Default.Normalized() with { AutoFilter = ColorFilter.Inverted };

        Assert.Equal(ColorFilter.Inverted, chosen.Normalized().AutoFilter);
        Assert.Equal(ColorFilter.Inverted, chosen.Normalized().Normalized().AutoFilter);
    }

    [Fact]
    public void Model_TakesTheMigratedValue()
    {
        var model = new WindowFilterModel(new WindowFilterOptions { AutoFilter = ColorFilter.Inverted });

        Assert.Equal(ColorFilter.InvertedSmart, model.Options.AutoFilter);
    }
}
