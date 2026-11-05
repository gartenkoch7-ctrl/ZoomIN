using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

public sealed class BrightnessTests
{
    private const uint White = 0xFFFFFFFF;
    private const uint Black = 0xFF000000;
    private static readonly PhysicalRect s_window = new(100, 100, 100, 100);
    private static readonly PhysicalRect[] s_screen = [new(0, 0, 1920, 1080)];

    [Fact]
    public void TwoOfThree_Decide()
    {
        var vote = default(BrightnessVote).Add(true);
        Assert.Equal(WindowBrightness.Pending, vote.Decision);

        vote = vote.Add(false).Add(true);
        Assert.Equal(WindowBrightness.Light, vote.Decision);
        Assert.Equal(WindowBrightness.Dark, default(BrightnessVote).Add(false).Add(false).Decision);
    }

    [Fact]
    public void Looks_FollowTheSchedule_AndRetryAfterFailures()
    {
        var vote = default(BrightnessVote);
        Assert.Equal(TimeSpan.FromMilliseconds(250), vote.NextLookAt);
        Assert.Equal(TimeSpan.FromMilliseconds(600), vote.Add(null).NextLookAt);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), vote.Add(null).Add(true).NextLookAt);
        Assert.Equal(TimeSpan.FromMilliseconds(1300), vote.Add(null).Add(null).Add(true).NextLookAt);
    }

    [Fact]
    public void AfterTheDeadline_ASingleResultDecides_ATieIsUnknown()
    {
        var late = BrightnessVote.Deadline;

        Assert.Equal(WindowBrightness.Pending, default(BrightnessVote).Add(true).ResultAfter(TimeSpan.FromSeconds(1)));
        Assert.Equal(WindowBrightness.Light, default(BrightnessVote).Add(true).Add(null).ResultAfter(late));
        Assert.Equal(WindowBrightness.Unknown, default(BrightnessVote).Add(true).Add(false).ResultAfter(late));
        Assert.Equal(WindowBrightness.Unknown, default(BrightnessVote).Add(null).ResultAfter(late));
    }

    [Theory]
    [InlineData(70, 60, true)]
    [InlineData(50, 60, false)]
    [InlineData(50, 45, true)]
    public void LightShare_IsComparedWithTheThreshold(int lightPercent, int threshold, bool expected)
    {
        var pixels = Rows(10, lightPercent / 10);

        Assert.Equal(expected, BrightnessMeter.IsLight(pixels, 10, 10, s_window, [], s_screen, threshold));
    }

    [Fact]
    public void CoveredParts_DoNotCount()
    {
        // Top 7 rows white, but a dark window above covers rows 0–5: only 1 of 4 visible rows is light.
        var pixels = Rows(10, 7);
        PhysicalRect[] above = [new(100, 100, 100, 60)];

        Assert.False(BrightnessMeter.IsLight(pixels, 10, 10, s_window, above, s_screen, 60));
    }

    [Fact]
    public void AlmostHiddenWindow_CannotBeMeasured()
    {
        PhysicalRect[] above = [new(100, 100, 100, 90)];

        Assert.Null(BrightnessMeter.IsLight(Rows(10, 10), 10, 10, s_window, above, s_screen, 60));
    }

    [Fact]
    public void PartsOutsideEveryMonitor_DoNotCount()
    {
        // The window hangs half off the left edge; the black left half lies outside the monitor.
        var pixels = new uint[100];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = i % 10 < 5 ? Black : White;
        }

        Assert.True(BrightnessMeter.IsLight(pixels, 10, 10, new PhysicalRect(-50, 0, 100, 100), [], s_screen, 60));
    }

    [Theory]
    [InlineData(0xFFFFFFFF, 255)]
    [InlineData(0xFF000000, 0)]
    [InlineData(0xFF00FF00, 182)]
    public void Luma_UsesRec709Weights(uint pixel, int expected)
    {
        Assert.Equal(expected, BrightnessMeter.Luma(pixel));
    }

    // 10 × 10 pixels, the first lightRows rows white, the rest black.
    private static uint[] Rows(int size, int lightRows)
    {
        var pixels = new uint[size * size];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = i / size < lightRows ? White : Black;
        }

        return pixels;
    }
}
