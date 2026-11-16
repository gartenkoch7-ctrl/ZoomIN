using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Core.Tests.SecureDesktop;

public sealed class SecureDesktopModeTests
{
    private static readonly DateTimeOffset Boot = new(2026, 11, 3, 8, 0, 0, TimeSpan.FromHours(1));

    [Fact]
    public void NeverGaveUp_IsNotDisabled() =>
        Assert.False(SecureDesktopMode.IsDisabledAfterCrashes(null, Boot));

    [Fact]
    public void GaveUpSinceBoot_IsDisabled() =>
        Assert.True(SecureDesktopMode.IsDisabledAfterCrashes(Boot.AddMinutes(5), Boot));

    [Fact]
    public void GaveUpBeforeBoot_IsNotDisabled() =>
        Assert.False(SecureDesktopMode.IsDisabledAfterCrashes(Boot.AddDays(-1), Boot));
}
