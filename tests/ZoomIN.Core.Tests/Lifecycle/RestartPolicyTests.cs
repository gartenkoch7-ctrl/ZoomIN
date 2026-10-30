using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Core.Tests.Lifecycle;

public sealed class RestartPolicyTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void CrashesWithinFiveMinutes_AllowThreeRestarts(int crashes, bool expected)
    {
        var times = Enumerable.Range(0, crashes).Select(i => s_now.AddSeconds(-30 * i));

        Assert.Equal(expected, RestartPolicy.ShouldRestart(times, s_now));
    }

    [Fact]
    public void OldCrashes_DoNotCount()
    {
        var times = new[]
        {
            s_now.AddMinutes(-20), s_now.AddMinutes(-15), s_now.AddMinutes(-10), s_now.AddMinutes(-6), s_now,
        };

        Assert.True(RestartPolicy.ShouldRestart(times, s_now));
    }

    [Fact]
    public void FutureTimestamps_AreIgnored()
    {
        // A clock change must not block restarts forever.
        var times = new[] { s_now.AddHours(1), s_now.AddHours(1), s_now.AddHours(1), s_now };

        Assert.True(RestartPolicy.ShouldRestart(times, s_now));
    }
}
