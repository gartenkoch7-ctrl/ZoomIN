using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>Fading of the three highlights under their own conditions (E1, 100 ms fade).</summary>
public sealed class MarkerAlphasTests
{
    private const long Frequency = 1000;

    [Fact]
    public void FirstUpdate_JumpsToTheTarget()
    {
        var alphas = new MarkerAlphas();

        var changed = alphas.Update(ShowCondition.Always, ShowCondition.Moving, ShowCondition.Idle, default, 10_000, Frequency);

        Assert.True(changed);
        Assert.Equal(1, alphas.Crosshair);
        Assert.Equal(0, alphas.Caret);
        Assert.Equal(1, alphas.Focus);
    }

    [Fact]
    public void FactorySetting_StaysFullyShown_WithoutRedraws()
    {
        var alphas = new MarkerAlphas();
        alphas.Update(ShowCondition.Always, ShowCondition.Always, ShowCondition.Always, default, 0, Frequency);

        var changed = alphas.Update(ShowCondition.Always, ShowCondition.Always, ShowCondition.Always, new(500, 0), 1000, Frequency);

        Assert.False(changed);
        Assert.Equal((1.0, 1.0, 1.0), (alphas.Crosshair, alphas.Caret, alphas.Focus));
    }

    [Fact]
    public void Typing_FadesTheIdleCaretOutIn100Ms()
    {
        var alphas = new MarkerAlphas();
        alphas.Update(ShowCondition.Always, ShowCondition.Idle, ShowCondition.Always, default, 10_000, Frequency);

        var typed = new MarkerActivitySample(10_000, 0);
        Assert.True(alphas.Update(ShowCondition.Always, ShowCondition.Idle, ShowCondition.Always, typed, 10_050, Frequency));
        Assert.Equal(0.5, alphas.Caret, 6);

        Assert.True(alphas.Update(ShowCondition.Always, ShowCondition.Idle, ShowCondition.Always, typed, 10_100, Frequency));
        Assert.Equal(0, alphas.Caret);

        Assert.False(alphas.Update(ShowCondition.Always, ShowCondition.Idle, ShowCondition.Always, typed, 10_150, Frequency));
    }

    [Fact]
    public void OneSecondAfterTyping_TheIdleCaretFadesIn()
    {
        var alphas = new MarkerAlphas();
        var typed = new MarkerActivitySample(10_000, 0);
        alphas.Update(ShowCondition.Always, ShowCondition.Idle, ShowCondition.Always, typed, 10_000, Frequency);
        Assert.Equal(0, alphas.Caret);

        alphas.Update(ShowCondition.Always, ShowCondition.Idle, ShowCondition.Always, typed, 11_000, Frequency);
        alphas.Update(ShowCondition.Always, ShowCondition.Idle, ShowCondition.Always, typed, 11_100, Frequency);

        Assert.Equal(1, alphas.Caret);
    }

    [Theory]
    [InlineData(ShowCondition.Always, 1)]
    [InlineData(ShowCondition.Idle, 0)]
    [InlineData(ShowCondition.Moving, 1)]
    [InlineData(ShowCondition.HoldKey, 0)]
    public void EachMarker_FollowsItsOwnCondition_WhileMoving(ShowCondition condition, double alpha)
    {
        var moving = new MarkerActivitySample(5_000, 0);
        const ShowCondition A = ShowCondition.Always;

        Assert.Equal((alpha, 1.0, 1.0), Alphas(condition, A, A, moving));
        Assert.Equal((1.0, alpha, 1.0), Alphas(A, condition, A, moving));
        Assert.Equal((1.0, 1.0, alpha), Alphas(A, A, condition, moving));
    }

    [Fact]
    public void HoldKey_ShowsOnlyThatMarker()
    {
        var held = new MarkerActivitySample(0, 4_000, 4_980);

        Assert.Equal((0.0, 1.0, 1.0), Alphas(ShowCondition.Idle, ShowCondition.HoldKey, ShowCondition.Always, new(4_900, 4_600, 4_980)));
        Assert.Equal((1.0, 1.0, 0.0), Alphas(ShowCondition.HoldKey, ShowCondition.Always, ShowCondition.Moving, held));
    }

    [Fact]
    public void FromOptions_HighlightsThatAreOff_StayHidden()
    {
        var alphas = new MarkerAlphas();
        var cursor = PointerStyle.Default with { CrosshairShow = ShowCondition.HoldKey }; // crosshair off
        var tracking = TrackingOptions.Default with
        {
            FocusFrame = false,
            FocusFrameShow = ShowCondition.HoldKey,
            CaretShow = ShowCondition.HoldKey,
        };

        alphas.Update(cursor, 4, tracking, default, 5_000, Frequency);

        Assert.Equal((0.0, 0.0, 0.0), (alphas.Crosshair, alphas.Caret, alphas.Focus));
    }

    [Fact]
    public void FromOptions_TrackingOff_HidesCaretAndFrame_NotTheCrosshair()
    {
        var alphas = new MarkerAlphas();
        var cursor = PointerStyle.Default with { CrosshairEnabled = true };
        var tracking = TrackingOptions.Default with { Enabled = false, CaretShow = ShowCondition.HoldKey };

        alphas.Update(cursor, 4, tracking, default, 5_000, Frequency);

        Assert.Equal((1.0, 0.0, 0.0), (alphas.Crosshair, alphas.Caret, alphas.Focus));
    }

    // Opacities after a first update at t = 5000 ms (no fade on the first frame).
    private static (double, double, double) Alphas(
        ShowCondition crosshair, ShowCondition caret, ShowCondition focus, MarkerActivitySample activity)
    {
        var alphas = new MarkerAlphas();
        alphas.Update(crosshair, caret, focus, activity, 5_000, Frequency);
        return (alphas.Crosshair, alphas.Caret, alphas.Focus);
    }
}
