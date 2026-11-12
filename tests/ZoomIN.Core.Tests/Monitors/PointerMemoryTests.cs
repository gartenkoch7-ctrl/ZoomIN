using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Monitors;

/// <summary>Where the pointer lands on a monitor (G2): its last position there, else the middle.</summary>
public sealed class PointerMemoryTests
{
    private const string First = @"\\.\DISPLAY1";
    private static readonly PhysicalRect s_first = new(0, 0, 1920, 1080);

    [Fact]
    public void Recall_GivesTheLastPosition_OrTheMiddle()
    {
        var memory = new PointerMemory();
        Assert.Equal(new PhysicalPoint(960, 540), memory.Recall(First, s_first));

        memory.Remember(First, new PhysicalPoint(100, 200));
        Assert.Equal(new PhysicalPoint(100, 200), memory.Recall(First, s_first));

        // The monitor moved or shrank: the old point is no longer on it.
        Assert.Equal(new PhysicalPoint(2880, 540), memory.Recall(First, new PhysicalRect(1920, 0, 1920, 1080)));
        Assert.Equal(new PhysicalPoint(1, 1), memory.Recall(First, new PhysicalRect(0, 0, 3, 3)));
    }

    [Fact]
    public void KeepOnly_ForgetsMonitorsThatAreGone()
    {
        var memory = new PointerMemory();
        memory.Remember(First, new PhysicalPoint(100, 200));
        memory.Remember(@"\\.\DISPLAY2", new PhysicalPoint(2000, 200));

        memory.KeepOnly([@"\\.\DISPLAY2"]);

        Assert.Equal(new PhysicalPoint(960, 540), memory.Recall(First, s_first));
        Assert.Throws<ArgumentNullException>(() => memory.Remember(null!, default));
    }

    [Theory]
    [InlineData(@"\\.\DISPLAY1", 1)]
    [InlineData(@"\\.\DISPLAY12", 12)]
    [InlineData("DISPLAY3", 3)]
    [InlineData("Monitor", 0)]
    [InlineData("", 0)]
    public void MonitorNumber_IsTheTrailingNumber(string id, int number) => Assert.Equal(number, MonitorNames.Number(id));
}
