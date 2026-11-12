using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>Input activity and hold key for the show conditions (E1), with a manual clock (ADR-0025).</summary>
public sealed class MarkerActivityTests
{
    private readonly ManualClock _clock = new();

    [Fact]
    public void MovingAndTyping_ResetTheRest()
    {
        var activity = new MarkerActivity(_clock);
        _clock.Now = 5000;
        activity.Moved();
        Assert.Equal(5000, activity.Sample.LastActivity);

        _clock.Now = 9000;
        activity.Typed();

        Assert.Equal(9000, activity.Sample.LastActivity);
        Assert.False(MarkerVisibility.IsVisible(ShowCondition.Idle, activity.Sample, 9500, ManualClock.Frequency));
        Assert.True(MarkerVisibility.IsVisible(ShowCondition.Idle, activity.Sample, 10_000, ManualClock.Frequency));
    }

    [Fact]
    public void HoldKey_KeyRepeatKeepsTheFirstPress()
    {
        var activity = new MarkerActivity(_clock);
        _clock.Now = 1000;
        activity.HoldKeyDown();
        _clock.Now = 1200;
        activity.HoldKeyDown();

        Assert.Equal(1000, activity.Sample.HoldSince);
        Assert.True(MarkerVisibility.IsVisible(ShowCondition.HoldKey, activity.Sample, 1250, ManualClock.Frequency));
    }

    [Fact]
    public void HoldKey_ReleaseHides()
    {
        var activity = new MarkerActivity(_clock);
        _clock.Now = 1000;
        activity.HoldKeyDown();
        activity.HoldKeyUp();

        Assert.Equal(0, activity.Sample.HoldSince);
    }

    [Fact]
    public void ShiftForACapitalLetter_IsNotAHold()
    {
        var activity = new MarkerActivity(_clock);
        _clock.Now = 1000;
        activity.HoldKeyDown();
        activity.OtherKeyDown();
        _clock.Now = 2000;
        activity.HoldKeyDown(); // key repeat while still held

        Assert.False(MarkerVisibility.IsVisible(ShowCondition.HoldKey, activity.Sample, 2000, ManualClock.Frequency));

        // Released and pressed again on its own: a hold.
        activity.HoldKeyUp();
        activity.HoldKeyDown();
        Assert.True(MarkerVisibility.IsVisible(ShowCondition.HoldKey, activity.Sample, 2300, ManualClock.Frequency));
    }

    [Fact]
    public void OtherKey_WithoutHoldKey_ChangesNothing()
    {
        var activity = new MarkerActivity(_clock);

        activity.OtherKeyDown();

        Assert.Equal(0, activity.Sample.HoldSince);
    }

    [Fact]
    public void MissedRelease_TheNextPressStartsAFreshHold()
    {
        var activity = new MarkerActivity(_clock);
        _clock.Now = 1000;
        activity.HoldKeyDown();
        activity.OtherKeyDown(); // spoiled, then the release is lost (Win+L)

        _clock.Now = 9000;
        activity.HoldKeyDown();

        Assert.Equal(9000, activity.Sample.HoldSince);
        Assert.False(MarkerVisibility.IsVisible(ShowCondition.HoldKey, activity.Sample, 9100, ManualClock.Frequency));
        Assert.True(MarkerVisibility.IsVisible(ShowCondition.HoldKey, activity.Sample, 9250, ManualClock.Frequency));
    }

    [Fact]
    public void ChangingTheKey_ForgetsARunningHold()
    {
        var activity = new MarkerActivity(_clock);
        _clock.Now = 1000;
        activity.HoldKeyDown();

        activity.Key = MarkerHoldKey.LeftAlt;
        activity.Key = MarkerHoldKey.LeftAlt; // same key again: nothing to forget

        Assert.Equal(0, activity.Sample.HoldSince);
    }

    [Fact]
    public void ConsumeHoldEnd_OnlyAfterARealHold_Once()
    {
        var activity = new MarkerActivity(_clock);
        _clock.Now = 1000;
        activity.HoldKeyDown();
        _clock.Now = 1100;
        activity.HoldKeyUp();
        Assert.False(activity.ConsumeHoldEnd()); // 100 ms tap: "Ctrl alone" stays a tap

        activity.HoldKeyDown();
        _clock.Now = 1500;
        activity.HoldKeyUp();
        Assert.True(activity.ConsumeHoldEnd());
        Assert.False(activity.ConsumeHoldEnd()); // a separate tap right after counts again
    }

    [Fact]
    public void ConsumeHoldEnd_TooLateAfterTheHold_IsATap()
    {
        var activity = new MarkerActivity(_clock);
        _clock.Now = 1000;
        activity.HoldKeyDown();
        _clock.Now = 1500;
        activity.HoldKeyUp();

        _clock.Now = 2100;
        Assert.False(activity.ConsumeHoldEnd());
    }

    [Fact]
    public void ConsumeHoldEnd_BeforeTheReleaseIsSeen_StillSuppressesOnce()
    {
        // The activity hook may report "Ctrl alone" before the hold key hook sees the release.
        var activity = new MarkerActivity(_clock);
        _clock.Now = 1000;
        activity.HoldKeyDown();
        _clock.Now = 1400;

        Assert.True(activity.ConsumeHoldEnd());
        activity.HoldKeyUp();
        Assert.False(activity.ConsumeHoldEnd());
    }

    [Fact]
    public void Key_IsShared_DefaultRightShift()
    {
        var activity = new MarkerActivity();
        Assert.Equal(MarkerHoldKey.RightShift, activity.Key);

        activity.Key = MarkerHoldKey.LeftCtrl;

        Assert.Equal(MarkerHoldKey.LeftCtrl, ((IHoldKeyHandler)activity).Key);
    }

    private sealed class ManualClock : TimeProvider
    {
        public const long Frequency = 1000;

        public long Now { get; set; }

        public override long TimestampFrequency => Frequency;

        public override long GetTimestamp() => Now;
    }
}
