using ZoomIN.Core.AgentDebug;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.AgentDebug;

public sealed class AgentSnapshotOptionsTests
{
    [Fact]
    public void WithoutArguments_ResultImagesOfAllMonitors_Unchanged()
    {
        Assert.True(AgentSnapshotOptions.TryParse(Request(), out var options, out _));

        Assert.Equal(["result"], options.Kinds);
        Assert.Null(options.Monitor);
        Assert.Null(options.Crop);
        Assert.Equal(0, options.MaxWidth);
    }

    [Fact]
    public void All_MeansEveryKind()
    {
        Assert.True(AgentSnapshotOptions.TryParse(Request(("images", "ALL")), out var options, out _));

        Assert.Equal(["result", "original", "weights"], options.Kinds);
    }

    [Fact]
    public void ParsesCropMonitorAndWidth()
    {
        Assert.True(AgentSnapshotOptions.TryParse(Request(("crop", " 10, 20,300 ,40"), ("monitor", @"\\.\DISPLAY2"), ("max_width", "800")), out var options, out _));

        Assert.Equal(new PhysicalRect(10, 20, 300, 40), options.Crop);
        Assert.Equal("DISPLAY2", options.Monitor);
        Assert.Equal(800, options.MaxWidth);
    }

    [Theory]
    [InlineData("images", "everything")]
    [InlineData("crop", "1,2,3")]
    [InlineData("crop", "1,2,0,4")]
    [InlineData("crop", "-1,2,3,4")]
    [InlineData("crop", "a,b,c,d")]
    [InlineData("max_width", "8")]
    [InlineData("max_width", "-100")]
    [InlineData("max_width", "wide")]
    public void WrongArguments_AreRefusedWithAReason(string name, string value)
    {
        Assert.False(AgentSnapshotOptions.TryParse(Request((name, value)), out _, out var error));
        Assert.Contains(name, error, StringComparison.Ordinal);
    }

    private static AgentRequest Request(params (string Name, string Value)[] arguments) =>
        new(AgentProtocol.Version, AgentTools.TakeSnapshot, arguments.ToDictionary(a => a.Name, a => a.Value));
}
