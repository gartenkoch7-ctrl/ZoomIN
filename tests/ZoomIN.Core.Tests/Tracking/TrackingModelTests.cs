using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>Option changes from several threads must not overwrite each other (M18 review).</summary>
public sealed class TrackingModelTests
{
    [Fact]
    public void UpdateOptions_ChangesOnlyItsField_AndRaisesChanged()
    {
        var model = new TrackingModel(TrackingOptions.Default);
        var changes = 0;
        model.Changed += () => changes++;
        model.ToggleEnabled();

        model.UpdateOptions(o => o with { CaretShow = ShowCondition.HoldKey });

        Assert.False(model.Options.Enabled);
        Assert.Equal(ShowCondition.HoldKey, model.Options.CaretShow);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void ConcurrentUpdates_AreAllKept()
    {
        var model = new TrackingModel(TrackingOptions.Default with { PanSeconds = 0 });

        Parallel.For(0, 1000, _ => model.UpdateOptions(o => o with { PanSeconds = o.PanSeconds + 1 }));

        Assert.Equal(1000, model.Options.PanSeconds);
    }
}
