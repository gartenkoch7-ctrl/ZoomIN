using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Zoom;

public sealed class ZoomModelTests
{
    private const string Left = @"\\.\DISPLAY1";
    private const string Right = @"\\.\DISPLAY2";
    private static readonly string[] s_both = [Left, Right];

    [Fact]
    public void StartsOff()
    {
        var model = Create();

        Assert.False(model.IsOn);
        Assert.Equal(1.0, model.TargetFor(Left));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    public void InvalidLevels_FallBackToDefault(double level)
    {
        var model = new ZoomModel(level, new Dictionary<string, double> { [Left] = level }, true, 0.15);

        Assert.Equal(ZoomLimits.DefaultLevel, model.SharedLastLevel);
        Assert.Equal(ZoomLimits.DefaultLevel, model.LastLevelFor(Left));
    }

    [Fact]
    public void Toggle_PerMonitor_UsesEachMonitorsLastLevel()
    {
        var model = Create(levels: new Dictionary<string, double> { [Left] = 3.0 });

        model.Toggle(s_both);

        Assert.Equal(3.0, model.TargetFor(Left));
        Assert.Equal(2.0, model.TargetFor(Right)); // no own level yet: shared start level
    }

    [Fact]
    public void Toggle_WhenAnyMonitorIsZoomed_SwitchesAllOff()
    {
        var model = Create();
        model.Step(1, Left, s_both);

        model.Toggle(s_both);

        Assert.False(model.IsOn);
        Assert.Equal(1.25, model.LastLevelFor(Left), 9);
    }

    [Fact]
    public void Step_PerMonitor_ChangesOnlyThatMonitor()
    {
        var model = Create();
        model.Toggle(s_both);

        model.Step(1, Left, s_both);

        Assert.Equal(2.5, model.TargetFor(Left), 9);
        Assert.Equal(2.0, model.TargetFor(Right), 9);
    }

    [Fact]
    public void Step_Shared_ChangesAllMonitors_AndSharedLevel()
    {
        var model = Create(perMonitor: false);
        model.Toggle(s_both);

        model.Step(1, Left, s_both);

        Assert.Equal(2.5, model.TargetFor(Right), 9);
        Assert.Equal(2.5, model.SharedLastLevel, 9);
    }

    [Fact]
    public void StepUpFromOff_StartsAtOne_StepDownToOne_StopsThatMonitor()
    {
        var model = Create();

        model.Step(1, Left, s_both);
        Assert.Equal(1.25, model.TargetFor(Left), 9);
        Assert.True(model.IsOn);

        model.Step(-1, Left, s_both);
        Assert.False(model.IsOn);
        Assert.Equal(1.25, model.LastLevelFor(Left), 9);
    }

    [Fact]
    public void Step_IsClampedAtMax()
    {
        var model = Create();

        model.Step(100, Left, s_both);

        Assert.Equal(ZoomLimits.Max, model.TargetFor(Left));
    }

    [Fact]
    public void Changed_IsRaised_OnlyOnRealChanges()
    {
        var model = Create();
        var raised = 0;
        model.Changed += () => raised++;

        model.Step(-1, Left, s_both); // already at 1×: no change
        model.Toggle(s_both);
        model.Step(1, Left, s_both);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void AnimationSeconds_IsNeverNegative()
    {
        var model = new ZoomModel(2, new Dictionary<string, double>(), true, -1) { AnimationSeconds = -5 };

        Assert.Equal(0, model.AnimationSeconds);
    }

    [Fact]
    public void ZoomLimits_Step_IsMultiplicative()
    {
        Assert.Equal(2.5, ZoomLimits.Step(2.0, 1), 9);
        Assert.Equal(1.6, ZoomLimits.Step(2.0, -1), 9);
    }

    private static ZoomModel Create(IReadOnlyDictionary<string, double>? levels = null, bool perMonitor = true) =>
        new(2.0, levels ?? new Dictionary<string, double>(), perMonitor, 0.15);
}
