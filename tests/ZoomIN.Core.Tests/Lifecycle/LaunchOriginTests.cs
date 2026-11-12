using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Core.Tests.Lifecycle;

/// <summary>When the tray app counts as started by Ease of Access and leaves its job (ADR-0030, spike A1).</summary>
public sealed class LaunchOriginTests
{
    private const uint KillOnClose = 0x2000;

    [Theory]
    [InlineData("ATBroker")]
    [InlineData("atbroker")]
    [InlineData("Utilman")]
    public void BrokerAsParent_IsAnEaseOfAccessStart(string parent)
    {
        var origin = new LaunchOrigin(InJob: true, LaunchOrigin.BreakawayOk, parent, LeftJob: false);

        Assert.True(origin.ByEaseOfAccess);
        Assert.True(origin.ShouldLeaveJob);
    }

    [Fact]
    public void JobWhoseCreatorHasExited_IsAnEaseOfAccessStart()
    {
        var origin = new LaunchOrigin(InJob: true, LaunchOrigin.SilentBreakawayOk, null, LeftJob: false);

        Assert.True(origin.ByEaseOfAccess);
        Assert.True(origin.ShouldLeaveJob);
    }

    [Fact]
    public void RestartInAJob_IsNot_ThoughItsParentHasExited()
    {
        // "--wait-for": the old version or the updater exits on purpose before we look.
        var origin = new LaunchOrigin(InJob: true, LaunchOrigin.BreakawayOk, null, LeftJob: false) { AfterRestart = true };

        Assert.False(origin.ByEaseOfAccess);
        Assert.False(origin.ShouldLeaveJob);
    }

    [Theory]
    [InlineData("explorer", true)]
    [InlineData("explorer", false)]
    [InlineData("WindowsTerminal", true)]
    public void StartMenuOrTerminal_IsNot_EvenInAJob(string parent, bool inJob)
    {
        var origin = new LaunchOrigin(inJob, inJob ? LaunchOrigin.BreakawayOk : 0, parent, LeftJob: false);

        Assert.False(origin.ByEaseOfAccess);
        Assert.False(origin.ShouldLeaveJob);
    }

    [Fact]
    public void JobWithoutBreakaway_CannotBeLeft()
    {
        var origin = new LaunchOrigin(InJob: true, KillOnClose, "ATBroker", LeftJob: false);

        Assert.True(origin.ByEaseOfAccess);
        Assert.False(origin.BreakawayAllowed);
        Assert.False(origin.ShouldLeaveJob);
    }

    [Fact]
    public void RelaunchedInstance_NeverRelaunchesAgain()
    {
        // Even if it is still in a job (the shell may have kept it there): no loop.
        var origin = new LaunchOrigin(InJob: true, LaunchOrigin.BreakawayOk, "ZoomIN", LeftJob: true);

        Assert.True(origin.ByEaseOfAccess);
        Assert.False(origin.ShouldLeaveJob);
    }

    [Fact]
    public void Unknown_IsANormalStart()
    {
        Assert.False(LaunchOrigin.Unknown.ByEaseOfAccess);
        Assert.False(LaunchOrigin.Unknown.ShouldLeaveJob);
    }

    [Fact]
    public void ExtractMarker_RemovesOnlyTheMarker()
    {
        var rest = LaunchOrigin.ExtractMarker(["--wait-for", "12", LaunchOrigin.LeftJobSwitch], out var leftJob);

        Assert.True(leftJob);
        Assert.Equal(["--wait-for", "12"], rest);
    }

    [Fact]
    public void ExtractMarker_WithoutMarker_KeepsTheArguments()
    {
        string[] args = ["--uninstall"];

        Assert.Same(args, LaunchOrigin.ExtractMarker(args, out var leftJob));
        Assert.False(leftJob);
    }
}
