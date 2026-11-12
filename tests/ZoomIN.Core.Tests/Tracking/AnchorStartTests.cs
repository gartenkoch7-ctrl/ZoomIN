using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>A pipeline rebuilt while its monitor was frozen (G2) goes on from the frozen view after the thaw.</summary>
public sealed class AnchorStartTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void WithoutTheMouse_TheViewStartsAtTheGivenAnchor_ElseInTheMiddle()
    {
        var resumed = new AnchorController();
        resumed.StartAt(new PhysicalPoint(300, 200));
        Assert.Equal(new PhysicalPoint(300, 200), resumed.Next(Input(mouseInside: false)));

        Assert.Equal(new PhysicalPoint(960, 540), new AnchorController().Next(Input(mouseInside: false)));
    }

    private static AnchorInput Input(bool mouseInside) => new(
        1, new PhysicalPoint(5000, 5000), mouseInside, s_monitor, ZoomMode.Fullscreen, 4, LensSize.Default, DockLayout.Default,
        TrackingSnapshot.Empty, TrackingOptions.Default);
}
