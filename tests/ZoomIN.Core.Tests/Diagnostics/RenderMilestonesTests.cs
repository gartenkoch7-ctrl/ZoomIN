using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Tests.Diagnostics;

public sealed class RenderMilestonesTests
{
    [Fact]
    public void Nothing_HappenedAtFirst()
    {
        var milestones = new RenderMilestones(TimeProvider.System);
        milestones.Arm();

        Assert.Equal(0, milestones.FirstCapture);
        Assert.Equal(0, milestones.FirstShown);
        Assert.Equal(0, milestones.ZoomReached);
        Assert.False(milestones.IsComplete);
    }

    [Fact]
    public void EachMilestone_KeepsItsFirstTime()
    {
        var milestones = new RenderMilestones(TimeProvider.System);
        milestones.Arm();

        milestones.Observe(captured: true, shown: false, zoomReached: false);
        var capture = milestones.FirstCapture;
        milestones.Observe(captured: true, shown: true, zoomReached: false);
        var shown = milestones.FirstShown;
        milestones.Observe(captured: true, shown: true, zoomReached: true);

        Assert.Equal(capture, milestones.FirstCapture);
        Assert.Equal(shown, milestones.FirstShown);
        Assert.True(capture > 0 && capture <= shown && shown <= milestones.ZoomReached);
        Assert.True(milestones.IsComplete);
    }

    // The tray app never arms them: its render threads skip the milestones from the first frame.
    [Fact]
    public void NotArmed_IsCompleteAtOnce() => Assert.True(new RenderMilestones(TimeProvider.System).IsComplete);

    [Fact]
    public void ThePipeline_IsOnlyRecordedWhenArmed()
    {
        var milestones = new RenderMilestones(TimeProvider.System);

        milestones.ObservePipeline();
        Assert.Equal(0, milestones.PipelineReady);

        milestones.Arm();
        milestones.ObservePipeline();
        Assert.True(milestones.PipelineReady > 0);
    }

    [Fact]
    public void ShownWithoutZoom_IsNotComplete()
    {
        var milestones = new RenderMilestones(TimeProvider.System);
        milestones.Arm();

        milestones.Observe(captured: true, shown: true, zoomReached: false);

        Assert.False(milestones.IsComplete);
    }
}
