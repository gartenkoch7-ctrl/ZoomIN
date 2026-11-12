using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>Show conditions of the highlights (E1): every condition, the 1 s rest limit and the hold key.</summary>
public sealed class MarkerVisibilityTests
{
    private const long Frequency = 1000; // ticks are milliseconds here
    private const long Now = 100_000;

    [Theory]
    [InlineData(ShowCondition.Always, true)]
    [InlineData(ShowCondition.Idle, true)]
    [InlineData(ShowCondition.Moving, false)]
    [InlineData(ShowCondition.HoldKey, false)]
    public void NoActivityYet(ShowCondition condition, bool visible)
    {
        Assert.Equal(visible, MarkerVisibility.IsVisible(condition, default, Now, Frequency));
    }

    [Theory]
    [InlineData(ShowCondition.Always, true)]
    [InlineData(ShowCondition.Idle, false)]
    [InlineData(ShowCondition.Moving, true)]
    [InlineData(ShowCondition.HoldKey, false)]
    public void JustMovedOrTyped(ShowCondition condition, bool visible)
    {
        Assert.Equal(visible, MarkerVisibility.IsVisible(condition, new(Now - 10, 0), Now, Frequency));
    }

    [Theory]
    [InlineData(999, false)]
    [InlineData(1000, true)]
    [InlineData(5000, true)]
    public void Idle_StartsAtExactlyOneSecond(long sinceActivity, bool visible)
    {
        var activity = new MarkerActivitySample(Now - sinceActivity, 0);

        Assert.Equal(visible, MarkerVisibility.IsVisible(ShowCondition.Idle, activity, Now, Frequency));
        Assert.Equal(!visible, MarkerVisibility.IsVisible(ShowCondition.Moving, activity, Now, Frequency));
    }

    [Theory]
    [InlineData(249, false)]
    [InlineData(250, true)]
    [InlineData(3000, true)]
    public void HoldKey_CountsAfterTheMinimumHoldTime(long held, bool visible)
    {
        // Key repeat keeps proving the key is down.
        var activity = new MarkerActivitySample(0, Now - held, Now - 30);

        Assert.Equal(visible, MarkerVisibility.IsVisible(ShowCondition.HoldKey, activity, Now, Frequency));
    }

    [Theory]
    [InlineData(1499, true)]
    [InlineData(1500, false)]
    public void HoldKey_WithoutKeyRepeat_TheReleaseWasMissed(long sinceLastDown, bool visible)
    {
        var activity = new MarkerActivitySample(0, Now - 5000, Now - sinceLastDown);

        Assert.Equal(visible, MarkerVisibility.IsVisible(ShowCondition.HoldKey, activity, Now, Frequency));
    }

    [Fact]
    public void HoldKey_SpoiledByTyping_StaysHidden()
    {
        Assert.False(MarkerVisibility.IsVisible(ShowCondition.HoldKey, new(Now, -1, Now), Now, Frequency));
    }

    [Fact]
    public void NeedsHoldKey_OnlyForSwitchedOnHighlights()
    {
        var cursor = PointerStyle.Default with { CrosshairShow = ShowCondition.HoldKey };
        var tracking = TrackingOptions.Default;

        Assert.False(MarkerVisibility.NeedsHoldKey(cursor, tracking)); // crosshair is off
        Assert.True(MarkerVisibility.NeedsHoldKey(cursor with { CrosshairEnabled = true }, tracking));
        Assert.True(MarkerVisibility.NeedsHoldKey(PointerStyle.Default, tracking with { CaretShow = ShowCondition.HoldKey }));
        Assert.True(MarkerVisibility.NeedsHoldKey(PointerStyle.Default, tracking with { FocusFrameShow = ShowCondition.HoldKey }));
        Assert.False(MarkerVisibility.NeedsHoldKey(
            PointerStyle.Default, tracking with { Enabled = false, CaretShow = ShowCondition.HoldKey }));
    }

    [Fact]
    public void UnknownCondition_IsAlwaysShown()
    {
        Assert.True(MarkerVisibility.IsVisible((ShowCondition)42, new(Now, 0), Now, Frequency));
    }
}
