using ZoomIN.Core.Filters;

namespace ZoomIN.Core.Tests.Filters;

/// <summary>The locator request takes its time from the injected clock, not from a static one (ADR-0025).</summary>
public sealed class EffectsModelClockTests
{
    [Fact]
    public void Locate_StampsTheInjectedClock()
    {
        var model = new EffectsModel(DisplayEffects.Default, new FixedClock(123_456));

        model.Locate();

        Assert.Equal(123_456, model.LocateRequestedAt);
    }

    [Fact]
    public void WithoutAClock_UsesTheSystemClock()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        var before = TimeProvider.System.GetTimestamp();

        model.Locate();

        Assert.InRange(model.LocateRequestedAt, before, TimeProvider.System.GetTimestamp());
    }

    private sealed class FixedClock(long timestamp) : TimeProvider
    {
        public override long GetTimestamp() => timestamp;
    }
}
