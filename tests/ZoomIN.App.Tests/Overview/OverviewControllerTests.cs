using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Tests.Overview;

/// <summary>The overview (B2) end to end without Windows: temporary 1×, keys, view requests, announcements.</summary>
public sealed class OverviewControllerTests
{
    private const string First = TwoMonitors.First;

    [Fact]
    public void Tap_ShowsOneX_WatchesKeys_AndAnnounces_WithoutTouchingTheUsersLevel()
    {
        using var rig = ZoomedRig();
        var before = rig.Zoom.TargetFor(First);

        rig.Controller.Press();
        rig.Overview.Release();

        Assert.Equal(OverviewPhase.Tapped, rig.Overview.Phase);
        Assert.Equal(ZoomLimits.Min, rig.Level());
        Assert.Equal(ZoomLimits.Min, rig.Zoom.EffectiveTargetFor(First));
        Assert.Equal(before, rig.Zoom.TargetFor(First));
        Assert.True(rig.Keys.Watching);
        Assert.Equal(HotkeyGesture.Overview, rig.Keys.Gesture);
        Assert.Equal(["Overview on"], rig.Announcer.Messages);
    }

    [Fact]
    public void SecondTap_ZoomsIn_CentredOnTheFrame_AndRestoresTheLevel()
    {
        using var rig = ZoomedRig();
        rig.Controller.Press();
        rig.Overview.Release();
        rig.Cursor.Position = new PhysicalPoint(100, 1000);

        rig.Controller.Press();

        Assert.Equal(OverviewPhase.Off, rig.Overview.Phase);
        Assert.Null(rig.Level());
        Assert.Equal(2, rig.Zoom.EffectiveTargetFor(First));
        Assert.False(rig.Keys.Watching);

        // 2× fullscreen: a 960×540 frame around the mouse, stopped at the bottom-left corner.
        var request = Assert.IsType<Core.Tracking.ViewCenterRequest>(rig.Models.ViewRequests.Latest);
        Assert.Equal(First, request.MonitorId);
        Assert.Equal(new PhysicalPoint(480, 810), request.Placement.Center);
        Assert.Equal(2, request.Placement.Zoom);
        Assert.Equal(["Overview on", "Overview off"], rig.Announcer.Messages);
    }

    [Fact]
    public void Hold_ZoomsIn_OnRelease()
    {
        using var rig = ZoomedRig();
        rig.Controller.Press();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(400));

        rig.Overview.Release();

        Assert.Equal(OverviewPhase.Off, rig.Overview.Phase);
        Assert.Null(rig.Level());
        Assert.NotNull(rig.Models.ViewRequests.Latest);
    }

    [Fact]
    public void Escape_ReturnsToTheRecordedView()
    {
        using var rig = ZoomedRig();
        rig.Controller.Press();
        rig.Overview.Current!.CaptureStart(new PhysicalRect(200, 100, 960, 540), new PhysicalPoint(500, 300));
        rig.Cursor.Position = new PhysicalPoint(1500, 900);

        var message = rig.Debug.Escape();

        Assert.Equal("Overview: off", message);
        Assert.Null(rig.Level());
        Assert.Equal(new PhysicalPoint(680, 370), rig.Models.ViewRequests.Latest!.Placement.Center);
        Assert.Equal("Nothing changed: no overview runs.", rig.Debug.Escape());
    }

    [Fact]
    public void ZoomOff_DuringTheOverview_EndsIt()
    {
        using var rig = ZoomedRig();
        rig.Controller.Press();

        rig.Commands.Toggle();

        Assert.Null(rig.Overview.Current);
        Assert.False(rig.Keys.Watching);
        Assert.Null(rig.Models.ViewRequests.Latest);
        Assert.Equal(["Overview on", "Overview off"], rig.Announcer.Messages);
    }

    [Fact]
    public void WheelStep_DuringTheOverview_ChangesTheUsersLevelInvisibly()
    {
        using var rig = ZoomedRig();
        rig.Debug.Tap();

        rig.Commands.ZoomBy(1);

        Assert.NotNull(rig.Overview.Current);
        Assert.Equal(ZoomLimits.Min, rig.Zoom.EffectiveTargetFor(First));
        Assert.Equal(2.5, rig.Zoom.TargetFor(First), 3);
        Assert.Contains("frame (", rig.Debug.Describe(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Press_DoesNothing_WhenNotZoomed_OrDocked(bool docked)
    {
        using var rig = new OverviewRig();
        if (docked)
        {
            rig.Commands.Activate(ZoomMode.Docked);
        }

        rig.Controller.Press();

        Assert.Null(rig.Overview.Current);
        Assert.False(rig.Keys.Watching);
        Assert.Empty(rig.Announcer.Messages);
    }

    [Fact]
    public async Task HookSignals_GoThroughTheThreadPool_AndStaleOnesAreIgnored()
    {
        using var rig = ZoomedRig();
        rig.Controller.Press();
        var handler = rig.Keys.Handler!;
        var id = rig.Overview.Current!.Id;

        handler.ShortcutReleased(id + 1, late: false);
        handler.ShortcutReleased(id, late: false);
        await WaitFor(() => rig.Overview.Phase == OverviewPhase.Tapped);

        handler.EscapePressed(id);
        await WaitFor(() => rig.Overview.Current is null && rig.Level() is null);

        Assert.Null(rig.Level());
    }

    [Fact]
    public async Task EscapeWhileHolding_ThenTheRelease_StaysAnEscape()
    {
        using var rig = ZoomedRig();
        rig.Controller.Press();
        rig.Clock.Advance(TimeSpan.FromSeconds(1)); // a hold
        var handler = rig.Keys.Handler!;
        var id = rig.Overview.Current!.Id;
        OverviewEnded? end = null;
        rig.Overview.Ended += e => end = e;

        handler.EscapePressed(id);
        handler.ShortcutReleased(id, late: false);
        await WaitFor(() => end is not null && rig.Level() is null);

        Assert.Equal(OverviewEnd.Escape, end?.End);
        Assert.True(end?.Held);
    }

    [Fact]
    public void Lens_StartsToo_AndAModeChangeEndsIt()
    {
        using var rig = new OverviewRig();
        rig.Commands.Activate(ZoomMode.Lens);
        rig.Debug.Tap();
        Assert.NotNull(rig.Overview.Current);

        rig.Commands.SelectMode(ZoomMode.Fullscreen);

        Assert.Null(rig.Overview.Current);
        Assert.Null(rig.Level());
    }

    private static OverviewRig ZoomedRig()
    {
        var rig = new OverviewRig();
        rig.Commands.Toggle();
        return rig;
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }
}
