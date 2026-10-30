using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Zoom;

public sealed class ZoomModelTests
{
    [Fact]
    public void StartsOff_WithGivenLastLevel()
    {
        var model = new ZoomModel(3.0, 0.15);

        Assert.False(model.IsOn);
        Assert.Equal(1.0, model.Target);
        Assert.Equal(3.0, model.LastLevel);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    public void InvalidLastLevel_FallsBackToDefault(double lastLevel)
    {
        var model = new ZoomModel(lastLevel, 0.15);

        Assert.Equal(ZoomLimits.DefaultLevel, model.LastLevel);
    }

    [Fact]
    public void Toggle_SwitchesBetweenOffAndLastLevel()
    {
        var model = new ZoomModel(3.0, 0.15);

        model.Toggle();
        Assert.Equal(3.0, model.Target);

        model.Toggle();
        Assert.Equal(1.0, model.Target);
        Assert.Equal(3.0, model.LastLevel);
    }

    [Fact]
    public void StepUpFromOff_StartsAtOneAndGrows()
    {
        var model = new ZoomModel(4.0, 0.15);

        model.Step(1);

        Assert.Equal(1.25, model.Target, 9);
        Assert.Equal(1.25, model.LastLevel, 9);
    }

    [Fact]
    public void StepDownToOne_SwitchesOff_AndKeepsLastLevelAboveOne()
    {
        var model = new ZoomModel(2.0, 0.15);
        model.Step(1);

        model.Step(-1);

        Assert.False(model.IsOn);
        Assert.Equal(1.25, model.LastLevel, 9);
    }

    [Fact]
    public void Step_IsClampedAtMax()
    {
        var model = new ZoomModel(2.0, 0.15);

        model.Step(100);

        Assert.Equal(ZoomLimits.Max, model.Target);
    }

    [Fact]
    public void Changed_IsRaised_OnlyWhenTargetChanges()
    {
        var model = new ZoomModel(2.0, 0.15);
        var raised = 0;
        model.Changed += () => raised++;

        model.Step(-1); // already off: no change
        model.Toggle();
        model.Step(1);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void AnimationSeconds_IsNeverNegative()
    {
        var model = new ZoomModel(2.0, -1) { AnimationSeconds = -5 };

        Assert.Equal(0, model.AnimationSeconds);
    }

    [Fact]
    public void ZoomLimits_Step_IsMultiplicative()
    {
        Assert.Equal(2.5, ZoomLimits.Step(2.0, 1), 9);
        Assert.Equal(1.6, ZoomLimits.Step(2.0, -1), 9);
    }
}
