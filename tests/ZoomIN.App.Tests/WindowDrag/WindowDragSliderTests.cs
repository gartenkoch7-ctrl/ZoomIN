using System.Globalization;
using System.Text.RegularExpressions;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.App.Tests.WindowDrag;

/// <summary>
/// The level slider on "Zoom &amp; Modi" must match the range the options normalize to; otherwise
/// the slider changes a loaded value on its own and it is saved unasked (G1 review).
/// </summary>
public sealed partial class WindowDragSliderTests
{
    [Fact]
    public void Slider_MatchesTheOptionsRange()
    {
        var page = File.ReadAllText(Path.Combine(RepoRoot(), "src", "ZoomIN.App", "Views", "Pages", "ZoomPage.xaml"));
        var slider = DragSlider().Match(page);

        Assert.True(slider.Success, "level slider of the drag card not found");
        Assert.Equal(WindowDragOptions.MaxLowerLevel, Number(slider, "max"));
        Assert.Equal(WindowDragOptions.MinLowerLevel, Number(slider, "min"));
        Assert.Equal(WindowDragOptions.LowerLevelStep, Number(slider, "step"));
    }

    private static double Number(Match match, string group) =>
        double.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    private static string RepoRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "ZoomIN.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("ZoomIN.slnx not found above the test folder");
    }

    [GeneratedRegex(@"Maximum=""(?<max>[\d.]+)"" Minimum=""(?<min>[\d.]+)"" Step=""(?<step>[\d.]+)"" Value=""\{x:Bind Drag\.LowerLevel")]
    private static partial Regex DragSlider();
}
