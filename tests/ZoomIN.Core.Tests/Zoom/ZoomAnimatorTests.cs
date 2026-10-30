using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Zoom;

public sealed class ZoomAnimatorTests
{
    private const double Duration = 0.15;

    [Fact]
    public void NewAnimator_IsSettledAtOne()
    {
        var animator = new ZoomAnimator();

        Assert.Equal(1.0, animator.Current);
        Assert.True(animator.IsSettled);
    }

    [Fact]
    public void ZeroDuration_JumpsImmediately()
    {
        var animator = new ZoomAnimator();
        animator.SetTarget(4.0);

        animator.Advance(1.0 / 60, 0);

        Assert.Equal(4.0, animator.Current);
    }

    [Fact]
    public void AfterDuration_About95PercentReached_AndSettlesSoonAfter()
    {
        var animator = new ZoomAnimator();
        animator.SetTarget(4.0);

        Run(animator, 60, Duration);
        var progress = Math.Log(animator.Current) / Math.Log(4.0);
        Run(animator, 60, Duration * 2);

        Assert.InRange(progress, 0.93, 0.97);
        Assert.True(animator.IsSettled);
    }

    [Fact]
    public void NeverOvershoots_WhenZoomingIn()
    {
        var animator = new ZoomAnimator();
        animator.SetTarget(16.0);

        for (var i = 0; i < 200; i++)
        {
            animator.Advance(1.0 / 144, Duration);
            Assert.True(animator.Current <= 16.0);
        }
    }

    [Fact]
    public void NeverUndershoots_WhenZoomingOut()
    {
        var animator = new ZoomAnimator();
        animator.SetTarget(8.0);
        animator.Advance(1, 0);
        animator.SetTarget(1.0);

        for (var i = 0; i < 200; i++)
        {
            animator.Advance(1.0 / 60, Duration);
            Assert.True(animator.Current >= 1.0);
        }

        Assert.True(animator.IsSettled);
    }

    [Fact]
    public void SameSpeed_At60And144Hz()
    {
        var at60 = new ZoomAnimator();
        var at144 = new ZoomAnimator();
        at60.SetTarget(8.0);
        at144.SetTarget(8.0);

        // 0.25 s = exactly 15 frames at 60 Hz and 36 frames at 144 Hz.
        Run(at60, 60, 0.25);
        Run(at144, 144, 0.25);

        Assert.Equal(at60.Current, at144.Current, 6);
    }

    [Fact]
    public void SetTarget_IsClamped()
    {
        var animator = new ZoomAnimator();

        animator.SetTarget(100);

        Assert.Equal(ZoomLimits.Max, animator.Target);
    }

    private static void Run(ZoomAnimator animator, int hz, double seconds)
    {
        var frames = (int)Math.Round(seconds * hz);
        for (var i = 0; i < frames; i++)
        {
            animator.Advance(1.0 / hz, Duration);
        }
    }
}
