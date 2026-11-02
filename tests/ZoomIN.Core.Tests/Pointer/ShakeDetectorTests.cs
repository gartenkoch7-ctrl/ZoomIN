using System.Diagnostics;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Tests.Pointer;

public sealed class ShakeDetectorTests
{
    private static long At(double seconds) => (long)(seconds * Stopwatch.Frequency);

    [Fact]
    public void QuickBackAndForth_IsAShake()
    {
        var detector = new ShakeDetector();

        var shaken = Feed(detector, strokes: 5, strokeLength: 80, secondsPerStroke: 0.1);

        Assert.True(shaken);
    }

    [Fact]
    public void SlowBackAndForth_IsNoShake()
    {
        var detector = new ShakeDetector();

        Assert.False(Feed(detector, strokes: 5, strokeLength: 80, secondsPerStroke: 0.5));
    }

    [Fact]
    public void ShortJitter_IsNoShake()
    {
        var detector = new ShakeDetector();

        Assert.False(Feed(detector, strokes: 10, strokeLength: 6, secondsPerStroke: 0.02));
    }

    [Fact]
    public void IdleDetector_FiresOnlyAfterThreeSecondsOfRest()
    {
        var idle = new IdleDetector();

        Assert.False(idle.Add(At(1)));
        Assert.False(idle.Add(At(2)));
        Assert.True(idle.Add(At(5.5)));
        Assert.False(idle.Add(At(5.6)));
    }

    // Moves the mouse back and forth in 10 px steps; true if a shake was recognised anywhere.
    private static bool Feed(ShakeDetector detector, int strokes, double strokeLength, double secondsPerStroke)
    {
        var x = 500.0;
        var time = 1.0;
        var direction = 1;
        var shaken = false;
        for (var s = 0; s < strokes; s++)
        {
            var steps = Math.Max(1, (int)(strokeLength / 2));
            for (var i = 0; i < steps; i++)
            {
                x += direction * strokeLength / steps;
                time += secondsPerStroke / steps;
                shaken |= detector.Add(At(time), x);
            }

            direction = -direction;
        }

        return shaken;
    }
}
