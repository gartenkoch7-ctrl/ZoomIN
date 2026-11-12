using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Tests.Pointer;

public sealed class PointerConstraintOptionsTests
{
    [Fact]
    public void Factory_BothOff()
    {
        var options = new AppSettings().PointerConstraint;

        Assert.Equal(EdgeBufferLevel.Off, options.Buffer);
        Assert.False(options.AxisLock);
        Assert.Equal(AxisLockKey.LeftShift, options.AxisLockKey);
    }

    [Theory]
    [InlineData(EdgeBufferLevel.Off, 0)]
    [InlineData(EdgeBufferLevel.Light, 200)]
    [InlineData(EdgeBufferLevel.Medium, 400)]
    [InlineData(EdgeBufferLevel.Strong, 800)]
    public void PressTime_FollowsTheLevel(EdgeBufferLevel level, int ms) =>
        Assert.Equal(TimeSpan.FromMilliseconds(ms), PointerConstraintOptions.PressTime(level));

    [Fact]
    public void Normalized_ReplacesUndefinedValues()
    {
        var options = new PointerConstraintOptions { Buffer = (EdgeBufferLevel)42, AxisLock = true, AxisLockKey = (AxisLockKey)(-1) };

        var normalized = options.Normalized();

        Assert.Equal(EdgeBufferLevel.Off, normalized.Buffer);
        Assert.Equal(AxisLockKey.LeftShift, normalized.AxisLockKey);
        Assert.True(normalized.AxisLock);
    }

    [Fact]
    public void Normalized_KeepsDefinedValues()
    {
        var options = new PointerConstraintOptions { Buffer = EdgeBufferLevel.Strong, AxisLockKey = AxisLockKey.RightAlt };

        Assert.Equal(options, options.Normalized());
    }

    [Fact]
    public void ProfileSwitch_KeepsTheGlobalValue()
    {
        var mine = new AppSettings { PointerConstraint = new PointerConstraintOptions { Buffer = EdgeBufferLevel.Medium } };

        var switched = ProfileFields.ApplyDisplay(mine, new AppSettings());

        Assert.Equal(EdgeBufferLevel.Medium, switched.PointerConstraint.Buffer);
    }
}
