using System.Diagnostics;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Tests.Diagnostics;

public sealed class TraceParentArgumentTests
{
    private const string Id = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    [Fact]
    public void Extract_RemovesSwitchAndValue_KeepsTheRest()
    {
        var rest = TraceParentArgument.Extract(["--watchdog", "42", TraceParentArgument.Switch, Id], out var parent);

        Assert.Equal(["--watchdog", "42"], rest);
        Assert.Equal(Id, parent);
    }

    [Fact]
    public void Extract_FirstPosition_LeavesExactPatternsWorking()
    {
        var rest = TraceParentArgument.Extract([TraceParentArgument.Switch, Id, "--secure-desktop"], out _);

        Assert.Equal(["--secure-desktop"], rest);
    }

    [Fact]
    public void Extract_WithoutSwitch_ReturnsSameArguments()
    {
        string[] args = ["--uninstall"];

        Assert.Same(args, TraceParentArgument.Extract(args, out var parent));
        Assert.Null(parent);
    }

    [Fact]
    public void Extract_SwitchWithoutValue_IsDropped()
    {
        Assert.Empty(TraceParentArgument.Extract([TraceParentArgument.Switch], out var parent));
        Assert.Null(parent);
    }

    [Fact]
    public void Append_WithoutCurrentOperation_ChangesNothing()
    {
        Activity.Current = null;

        Assert.Equal("--watchdog 1", TraceParentArgument.Append("--watchdog 1"));
    }

    [Fact]
    public void Append_InsideOperation_AddsItsW3CId()
    {
        using var activity = new Activity("zoomin.test.run").SetIdFormat(ActivityIdFormat.W3C).Start();

        Assert.Equal($"--watchdog 1 {TraceParentArgument.Switch} {activity.Id}", TraceParentArgument.Append("--watchdog 1"));
        Assert.Equal($"{TraceParentArgument.Switch} {activity.Id}", TraceParentArgument.Append(string.Empty));
    }
}
