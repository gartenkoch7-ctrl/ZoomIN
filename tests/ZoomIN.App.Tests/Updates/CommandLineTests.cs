using ZoomIN.App.SecureDesktop;
using ZoomIN.App.Updates;
using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.App.Tests.Updates;

/// <summary>Arguments of the short-lived roles; only exact forms start them (M14 step 8).</summary>
public sealed class CommandLineTests
{
    [Fact]
    public void RestartArguments_RoundTrip()
    {
        var args = RestartArguments.Format(4321).Split(' ');

        Assert.True(RestartArguments.TryParse(args, out var processId));
        Assert.Equal(4321, processId);
    }

    [Theory]
    [InlineData("--wait-for")]
    [InlineData("--wait-for", "-1")]
    [InlineData("--wait-for", "12", "extra")]
    [InlineData("--other", "12")]
    public void RestartArguments_RejectOtherForms(params string[] args)
    {
        Assert.False(RestartArguments.TryParse(args, out _));
    }

    [Theory]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void SecureDesktopSetting_ReadsOnAndOff(string value, bool expected)
    {
        Assert.True(SecureDesktopSetting.TryParse([SecureDesktopMode.SettingSwitch, value], out var on));
        Assert.Equal(expected, on);
    }

    [Fact]
    public void SecureDesktopSetting_RejectsAnythingElse()
    {
        Assert.False(SecureDesktopSetting.TryParse([SecureDesktopMode.SettingSwitch, "maybe"], out _));
        Assert.False(SecureDesktopSetting.TryParse([SecureDesktopMode.SettingSwitch], out _));
    }
}
