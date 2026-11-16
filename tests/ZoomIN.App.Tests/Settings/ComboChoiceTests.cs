using ZoomIN.App.ViewModels;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Tests.Settings;

/// <summary>ComboBox index to enum choice: −1 and unknown indexes keep the current value.</summary>
public sealed class ComboChoiceTests
{
    [Theory]
    [InlineData(-1, ShowCondition.Idle)]
    [InlineData(99, ShowCondition.Idle)]
    [InlineData(3, ShowCondition.HoldKey)]
    [InlineData(0, ShowCondition.Always)]
    public void Or_TakesKnownIndexes_KeepsTheCurrentValueOtherwise(int index, ShowCondition expected)
    {
        Assert.Equal(expected, ComboChoice.Or(index, ShowCondition.Idle));
    }
}
