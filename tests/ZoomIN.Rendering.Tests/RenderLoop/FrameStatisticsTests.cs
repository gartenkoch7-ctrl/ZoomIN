using System.Diagnostics;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>
/// Status frame rate with idle passes (M14 step 3): a resting zoom is on time, not "0 fps". Records
/// on the shared meter, so it runs with the other meter tests.
/// </summary>
[Collection(MeterTests.Name)]
public sealed class FrameStatisticsTests
{
    [Fact]
    public void PresentedAndIdlePasses_CountForTheStatus_MissedOnesDoNot()
    {
        var stats = new FrameStatistics(new RenderMetrics(@"\\.\DISPLAY9"));
        stats.Woke(Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 2));

        stats.Record(FrameOutcome.Presented, becameVisible: true);
        stats.Record(FrameOutcome.Idle, becameVisible: false);
        stats.Record(FrameOutcome.Idle, becameVisible: false);
        stats.Record(FrameOutcome.Missed, becameVisible: false);

        Assert.True(stats.SecondElapsed(out var frames));
        Assert.Equal(3, frames);
        Assert.False(stats.SecondElapsed(out _));
    }
}
