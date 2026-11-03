using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Core.Tests.SecureDesktop;

public sealed class LocalPathRuleTests
{
    [Theory]
    [InlineData(@"C:\Users\Anna\AppData\Roaming\ZoomIN")]
    [InlineData(@"d:\Profiles\anna\AppData\Roaming\ZoomIN\settings.json")]
    public void DriveLetterPaths_Pass(string path) => Assert.True(LocalPathRule.IsDriveLetterPath(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"\\server\share\ZoomIN")]
    [InlineData(@"\\attacker@80\x\ZoomIN")]
    [InlineData(@"\\?\UNC\server\share\ZoomIN")]
    [InlineData(@"\\?\C:\Users\Anna")]
    [InlineData(@"\\.\pipe\x")]
    [InlineData(@"C:relative")]
    [InlineData(@"ZoomIN\settings.json")]
    [InlineData(@"C:/Users/Anna")]
    [InlineData(@"C:\Users\Anna\\ZoomIN")]
    [InlineData(@"C:\Users\Anna\file.json:stream")]
    public void EverythingElse_IsRejected(string? path) => Assert.False(LocalPathRule.IsDriveLetterPath(path));
}
