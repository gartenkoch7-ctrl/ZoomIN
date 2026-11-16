using ZoomIN.Core.Updates;

namespace ZoomIN.Core.Tests.Updates;

public sealed class InstallChecklistTests
{
    [Fact]
    public void NewList_IsAllPending()
    {
        var list = new InstallChecklist();

        Assert.All(InstallChecklist.Steps, step => Assert.Equal(InstallStepState.Pending, list.StateOf(step)));
        Assert.Null(list.Running);
    }

    [Fact]
    public void Begin_TicksEarlierSteps()
    {
        var list = new InstallChecklist();
        list.Begin(InstallStep.RemoveOld);
        list.Begin(InstallStep.Install);

        Assert.Equal(InstallStepState.Done, list.StateOf(InstallStep.RemoveOld));
        Assert.Equal(InstallStepState.Done, list.StateOf(InstallStep.Download));
        Assert.Equal(InstallStepState.Running, list.StateOf(InstallStep.Install));
        Assert.Equal(InstallStepState.Pending, list.StateOf(InstallStep.Configure));
        Assert.Equal(InstallStep.Install, list.Running);
    }

    [Fact]
    public void FailedStep_KeepsLaterStepsPending()
    {
        var list = new InstallChecklist();
        list.Begin(InstallStep.Download);
        list.Finish(succeeded: false);

        Assert.Equal(InstallStepState.Done, list.StateOf(InstallStep.RemoveOld));
        Assert.Equal(InstallStepState.Failed, list.StateOf(InstallStep.Download));
        Assert.Equal(InstallStepState.Pending, list.StateOf(InstallStep.Install));
        Assert.Null(list.Running);
    }

    [Fact]
    public void Success_TicksEverything()
    {
        var list = new InstallChecklist();
        list.Begin(InstallStep.Configure);
        list.Finish(succeeded: true);

        Assert.All(InstallChecklist.Steps, step => Assert.Equal(InstallStepState.Done, list.StateOf(step)));
    }

    [Fact]
    public void Reset_RaisesChangedAndClears()
    {
        var list = new InstallChecklist();
        var changes = 0;
        list.Changed += () => changes++;
        list.Begin(InstallStep.Install);
        list.Reset();

        Assert.Equal(2, changes);
        Assert.Equal(InstallStepState.Pending, list.StateOf(InstallStep.RemoveOld));
    }
}
