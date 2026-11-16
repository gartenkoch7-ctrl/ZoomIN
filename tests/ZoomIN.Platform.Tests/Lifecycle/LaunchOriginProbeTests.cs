using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>Reads the test process itself: only what holds on every machine is checked.</summary>
public sealed class LaunchOriginProbeTests
{
    [Fact]
    public void Read_FindsTheLivingParent_ByNameOnly_AndPassesTheMarkerOn()
    {
        var origin = new LaunchOriginProbe().Read(leftJob: true);

        // The test host is started by dotnet or an IDE, which still runs and is older than it.
        Assert.False(string.IsNullOrEmpty(origin.ParentProcess));
        Assert.DoesNotContain('\\', origin.ParentProcess);
        Assert.DoesNotContain(".exe", origin.ParentProcess, StringComparison.OrdinalIgnoreCase);
        Assert.True(origin.LeftJob);
    }

    [Fact]
    public void Read_OutsideAJob_ReportsNoLimits()
    {
        var origin = new LaunchOriginProbe().Read(leftJob: false);

        Assert.True(origin.InJob || origin.JobLimits == 0);
        Assert.False(origin.LeftJob);
    }
}
