using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>"Centre the view on P" requests (M17), handed to the anchor as <see cref="ViewPlacement"/>.</summary>
public sealed class ViewCenterRequestsTests
{
    private const string First = @"\\.\DISPLAY1";
    private const string Second = @"\\.\DISPLAY2";

    [Fact]
    public void Request_IsSeenOnlyByItsMonitor()
    {
        var requests = new ViewCenterRequests();
        Assert.Null(requests.Latest);
        Assert.Equal(default, requests.PlacementFor(First));

        requests.Request(First, new PhysicalPoint(100, 200), 4, 1_000);

        Assert.Equal(new ViewPlacement(new PhysicalPoint(100, 200), 4, 1_000), requests.PlacementFor(First));
        Assert.Equal(default, requests.PlacementFor(Second));
        Assert.Equal(new ViewCenterRequest(First, new ViewPlacement(new PhysicalPoint(100, 200), 4, 1_000)), requests.Latest);
    }

    [Fact]
    public void LatestRequestWins_AndIsAlwaysNewer()
    {
        var requests = new ViewCenterRequests();
        requests.Request(First, new PhysicalPoint(1, 1), 2, 500);

        // Same or older clock reading (another thread): still news for the anchor.
        requests.Request(First, new PhysicalPoint(2, 2), 3, 500);

        var placement = requests.PlacementFor(First);
        Assert.Equal(new PhysicalPoint(2, 2), placement.Center);
        Assert.Equal(3, placement.Zoom);
        Assert.Equal(501, placement.At);
        Assert.Throws<ArgumentNullException>(() => requests.Request(null!, default, 2, 1));
    }
}
