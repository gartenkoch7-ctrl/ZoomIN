using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Tests.Pointer;

public sealed class RgbColorTests
{
    [Fact]
    public void PresetName_NamesEveryPreset_InOrder()
    {
        Assert.Equal(
            ["Yellow", "Green", "Cyan", "Magenta", "White", "Black"],
            RgbColor.Presets.Select(RgbColor.PresetName));
    }

    [Fact]
    public void PresetName_OtherColour_IsNull()
    {
        Assert.Null(RgbColor.PresetName(RgbColor.Amber));
    }
}
