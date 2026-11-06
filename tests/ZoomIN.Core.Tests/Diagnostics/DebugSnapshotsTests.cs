using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Tests.Diagnostics;

public sealed class DebugSnapshotsTests
{
    [Fact]
    public void NoRequest_NothingPending()
    {
        Assert.Null(new DebugSnapshots().Pending);
    }

    [Fact]
    public void EveryRequest_GetsANewGeneration_AndIsThePendingOne()
    {
        var snapshots = new DebugSnapshots();

        var first = snapshots.Request("a");
        var second = snapshots.Request("b");

        Assert.True(second.Generation > first.Generation);
        Assert.Same(second, snapshots.Pending);
        Assert.Equal("b", snapshots.Pending!.Folder);
    }

    [Fact]
    public void Report_ReachesTheListeners()
    {
        var snapshots = new DebugSnapshots();
        DebugSnapshotResult? seen = null;
        snapshots.Completed += r => seen = r;

        snapshots.Report(new DebugSnapshotResult(1, "DISPLAY1", true, "saved"));

        Assert.Equal("DISPLAY1", seen?.Monitor);
    }
}
