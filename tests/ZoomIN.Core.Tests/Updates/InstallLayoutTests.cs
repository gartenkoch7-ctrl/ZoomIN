using ZoomIN.Core.Updates;

namespace ZoomIN.Core.Tests.Updates;

public sealed class InstallLayoutTests
{
    [Theory]
    [InlineData(@"C:\Program Files\ZoomIN\current\ZoomIN.exe")]
    [InlineData(@"C:\Program Files\ZoomIN\app-1.1.0-dev.42\ZoomIN.exe")]
    [InlineData(@"C:\Program Files\ZoomIN\CURRENT\ZoomIN.exe")]
    public void VersionedInstall_IsRecognised(string exe)
    {
        Assert.True(InstallLayout.TryFromExecutable(exe, out var layout));
        Assert.Equal(@"C:\Program Files\ZoomIN", layout.Root);
        Assert.Equal(@"C:\Program Files\ZoomIN\current\ZoomIN.exe", layout.CurrentExecutable);
    }

    [Theory]
    [InlineData(@"C:\Program Files\ZoomIN\ZoomIN.exe")]
    [InlineData(@"D:\Projekte\ZoomIN\src\ZoomIN.App\bin\Debug\win-x64\ZoomIN.exe")]
    [InlineData(@"C:\Tools\app-latest\ZoomIN.exe")]
    [InlineData(@"ZoomIN.exe")]
    [InlineData(null)]
    public void OtherLocations_CannotUpdate(string? exe)
    {
        Assert.False(InstallLayout.TryFromExecutable(exe, out _));
    }

    [Fact]
    public void VersionFolder_IsNamedAfterVersion()
    {
        var layout = new InstallLayout(@"C:\Program Files\ZoomIN");

        Assert.Equal(@"C:\Program Files\ZoomIN\app-1.0.0-rc1", layout.VersionFolder(new AppVersion(1, 0, 0, "rc1")));
    }

    [Fact]
    public void Cleanup_KeepsActiveAndPreviousVersion()
    {
        string[] folders = ["app-1.0.0", "app-1.0.1", "app-1.1.0", "app-1.2.0.tmp", "current", "logs"];

        var remove = InstallLayout.FoldersToRemove(folders, new AppVersion(1, 1, 0), new AppVersion(1, 0, 1));

        Assert.Equal(["app-1.0.0", "app-1.2.0.tmp"], remove);
    }

    [Fact]
    public void Cleanup_WithoutPreviousVersion_KeepsOnlyActive()
    {
        var remove = InstallLayout.FoldersToRemove(["app-1.0.0", "app-1.1.0"], new AppVersion(1, 1, 0), previous: null);

        Assert.Equal(["app-1.0.0"], remove);
    }
}
